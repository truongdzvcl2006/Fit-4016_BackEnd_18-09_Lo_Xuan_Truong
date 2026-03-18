using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RealEstateAuction.Services
{
    public class BiddingEngine
    {
        // Dictionary lưu trữ các phiên đấu giá đang hoạt động
        private static readonly ConcurrentDictionary<int, AuctionSession> _activeAuctions = new();

        // Timer để kiểm tra thời gian kết thúc
        private Timer _timer;
        private readonly IServiceProvider _serviceProvider;

        public BiddingEngine(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            // Kiểm tra mỗi giây
            _timer = new Timer(CheckAuctions, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        // Cấu hình phiên đấu giá
        public class AuctionConfig
        {
            public decimal StartingPrice { get; set; }      // Giá khởi điểm
            public decimal? FloorPrice { get; set; }        // Giá sàn (tối thiểu)
            public decimal? CeilingPrice { get; set; }      // Giá trần (tối đa)
            public decimal MinBidStep { get; set; } = 100000; // Bước giá tối thiểu
            public decimal MaxBidStep { get; set; } = 1000000; // Bước giá tối đa
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public int AutoExtendSeconds { get; set; } = 60; // Tự động kéo dài khi có đấu giá vào phút cuối
        }

        // Thông tin phiên đấu giá
        public class AuctionSession
        {
            public int AuctionId { get; set; }
            public string PropertyName { get; set; }
            public decimal CurrentPrice { get; set; }
            public AuctionConfig Config { get; set; }
            public List<BidInfo> Bids { get; set; } = new();
            public BidInfo HighestBid => Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
            public DateTime LastBidTime { get; set; }
            public AuctionStatus Status { get; set; } = AuctionStatus.Upcoming;
            public int BidCount => Bids.Count;
        }

        public class BidInfo
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public string UserName { get; set; }
            public decimal Amount { get; set; }
            public DateTime BidTime { get; set; }
            public bool IsAutoBid { get; set; }
        }

        public enum AuctionStatus
        {
            Upcoming,      // Sắp diễn ra
            Active,        // Đang diễn ra
            Extended,      // Đã kéo dài
            Ended,         // Đã kết thúc
            Cancelled      // Đã hủy
        }

        // Khởi tạo phiên đấu giá mới
        public async Task<AuctionSession> InitializeAuction(int auctionId, string propertyName, AuctionConfig config)
        {
            var session = new AuctionSession
            {
                AuctionId = auctionId,
                PropertyName = propertyName,
                CurrentPrice = config.StartingPrice,
                Config = config,
                Status = config.StartTime > DateTime.Now ? AuctionStatus.Upcoming : AuctionStatus.Active,
                LastBidTime = config.StartTime
            };

            _activeAuctions[auctionId] = session;
            return session;
        }

        // Xử lý đấu giá
        public async Task<BidResult> PlaceBid(int auctionId, int userId, string userName, decimal amount, bool isAutoBid = false)
        {
            var result = new BidResult();

            if (!_activeAuctions.TryGetValue(auctionId, out var session))
            {
                result.Success = false;
                result.Message = "Phiên đấu giá không tồn tại";
                return result;
            }

            // Kiểm tra trạng thái phiên
            if (session.Status != AuctionStatus.Active && session.Status != AuctionStatus.Extended)
            {
                result.Success = false;
                result.Message = "Phiên đấu giá chưa bắt đầu hoặc đã kết thúc";
                return result;
            }

            // Kiểm tra giá đấu
            var validationResult = ValidateBid(session, amount);
            if (!validationResult.IsValid)
            {
                result.Success = false;
                result.Message = validationResult.Message;
                result.SuggestedPrice = validationResult.SuggestedPrice;
                return result;
            }

            // Kiểm tra giá trần
            if (session.Config.CeilingPrice.HasValue && amount > session.Config.CeilingPrice.Value)
            {
                result.Success = false;
                result.Message = $"Giá đấu vượt quá giá trần ({session.Config.CeilingPrice:N0} đ)";
                result.SuggestedPrice = session.Config.CeilingPrice.Value;
                return result;
            }

            // Tạo bid mới
            var bid = new BidInfo
            {
                Id = session.Bids.Count + 1,
                UserId = userId,
                UserName = userName,
                Amount = amount,
                BidTime = DateTime.Now,
                IsAutoBid = isAutoBid
            };

            session.Bids.Add(bid);
            session.CurrentPrice = amount;
            session.LastBidTime = DateTime.Now;

            // Kiểm tra và tự động kéo dài thời gian nếu đấu giá vào phút cuối
            if ((session.Config.EndTime - DateTime.Now).TotalSeconds <= session.Config.AutoExtendSeconds)
            {
                session.Config.EndTime = session.Config.EndTime.AddSeconds(session.Config.AutoExtendSeconds);
                session.Status = AuctionStatus.Extended;
                result.TimeExtended = true;
                result.NewEndTime = session.Config.EndTime;
            }

            result.Success = true;
            result.Message = "Đấu giá thành công";
            result.CurrentPrice = amount;
            result.BidId = bid.Id;
            result.HighestBidder = userName;

            return result;
        }

        // Validate giá đấu
        private (bool IsValid, string Message, decimal? SuggestedPrice) ValidateBid(AuctionSession session, decimal amount)
        {
            // Kiểm tra giá sàn
            if (session.Config.FloorPrice.HasValue && amount < session.Config.FloorPrice.Value)
            {
                return (false, $"Giá đấu phải >= giá sàn ({session.Config.FloorPrice:N0} đ)", session.Config.FloorPrice.Value);
            }

            // Kiểm tra bước giá tối thiểu
            if (amount < session.CurrentPrice + session.Config.MinBidStep)
            {
                var suggested = session.CurrentPrice + session.Config.MinBidStep;
                return (false, $"Giá đấu phải >= {suggested:N0} đ (giá hiện tại + bước giá tối thiểu)", suggested);
            }

            // Kiểm tra bước giá tối đa
            if (amount > session.CurrentPrice + session.Config.MaxBidStep)
            {
                var suggested = session.CurrentPrice + session.Config.MinBidStep;
                return (false, $"Bước giá không được vượt quá {session.Config.MaxBidStep:N0} đ. Gợi ý: {suggested:N0} đ", suggested);
            }

            return (true, null, null);
        }

        // Kiểm tra các phiên đấu giá
        private async void CheckAuctions(object state)
        {
            var now = DateTime.Now;
            var auctionsToEnd = new List<int>();

            foreach (var kvp in _activeAuctions)
            {
                var session = kvp.Value;

                // Cập nhật trạng thái
                if (session.Status == AuctionStatus.Upcoming && now >= session.Config.StartTime)
                {
                    session.Status = AuctionStatus.Active;
                }

                // Kiểm tra kết thúc
                if ((session.Status == AuctionStatus.Active || session.Status == AuctionStatus.Extended)
                    && now >= session.Config.EndTime)
                {
                    session.Status = AuctionStatus.Ended;
                    auctionsToEnd.Add(kvp.Key);
                }
            }
        }

        // Lấy thông tin phiên đấu giá
        public AuctionSession GetAuctionInfo(int auctionId)
        {
            _activeAuctions.TryGetValue(auctionId, out var session);
            return session;
        }

        // Lấy tất cả phiên đang hoạt động
        public List<AuctionSession> GetActiveAuctions()
        {
            return _activeAuctions.Values
                .Where(a => a.Status == AuctionStatus.Active || a.Status == AuctionStatus.Extended)
                .ToList();
        }

        // Kết thúc sớm phiên đấu giá
        public async Task<bool> EndAuctionEarly(int auctionId)
        {
            if (_activeAuctions.TryGetValue(auctionId, out var session))
            {
                session.Status = AuctionStatus.Ended;
                session.Config.EndTime = DateTime.Now;
                return true;
            }
            return false;
        }

        // Cập nhật cấu hình đấu giá
        public async Task<bool> UpdateAuctionConfig(int auctionId, AuctionConfig newConfig)
        {
            if (_activeAuctions.TryGetValue(auctionId, out var session))
            {
                session.Config = newConfig;
                return true;
            }
            return false;
        }

        // Kéo dài thời gian đấu giá
        public async Task<bool> ExtendAuctionTime(int auctionId, int extraSeconds)
        {
            if (_activeAuctions.TryGetValue(auctionId, out var session))
            {
                session.Config.EndTime = session.Config.EndTime.AddSeconds(extraSeconds);
                session.Status = AuctionStatus.Extended;
                return true;
            }
            return false;
        }
    }

    // Kết quả đấu giá
    public class BidResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public decimal? CurrentPrice { get; set; }
        public int? BidId { get; set; }
        public string HighestBidder { get; set; }
        public bool TimeExtended { get; set; }
        public DateTime? NewEndTime { get; set; }
        public decimal? SuggestedPrice { get; set; }
    }
}