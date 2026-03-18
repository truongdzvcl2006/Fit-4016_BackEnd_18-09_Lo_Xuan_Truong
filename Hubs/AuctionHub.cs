using Microsoft.AspNetCore.SignalR;
using RealEstateAuction.Services;
using System;
using System.Threading.Tasks;

namespace RealEstateAuction.Hubs
{
    public class AuctionHub : Hub
    {
        private readonly BiddingEngine _biddingEngine;

        public AuctionHub(BiddingEngine biddingEngine)
        {
            _biddingEngine = biddingEngine;
        }

        // Tham gia phòng đấu giá
        public async Task JoinAuctionRoom(int auctionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"auction-{auctionId}");

            var session = _biddingEngine.GetAuctionInfo(auctionId);
            if (session != null)
            {
                // SỬA: PropertName -> PropertyName
                await Clients.Caller.SendAsync("AuctionInfo", new
                {
                    session.AuctionId,
                    PropertyName = session.PropertyName, // Sửa lỗi chính tả
                    session.CurrentPrice,
                    session.Config.StartTime,
                    session.Config.EndTime,
                    Status = session.Status.ToString(),
                    session.BidCount,
                    HighestBidder = session.HighestBid?.UserName,
                    TimeRemaining = (session.Config.EndTime - DateTime.Now).TotalSeconds
                });

                // Gửi lịch sử đấu giá
                await Clients.Caller.SendAsync("BidHistory", session.Bids);
            }
        }

        // Rời phòng đấu giá
        public async Task LeaveAuctionRoom(int auctionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"auction-{auctionId}");
        }

        // Đặt giá
        public async Task PlaceBid(int auctionId, int userId, string userName, decimal amount)
        {
            var result = await _biddingEngine.PlaceBid(auctionId, userId, userName, amount);

            // Gửi kết quả cho người đặt
            await Clients.Caller.SendAsync("BidResult", result);

            if (result.Success)
            {
                // Thông báo cho tất cả trong phòng
                await Clients.Group($"auction-{auctionId}").SendAsync("PriceUpdated", new
                {
                    auctionId,
                    result.CurrentPrice,
                    result.HighestBidder,
                    result.TimeExtended,
                    result.NewEndTime,
                    Bidder = userName,
                    Amount = amount
                });

                // Nếu thời gian được kéo dài
                if (result.TimeExtended)
                {
                    await Clients.Group($"auction-{auctionId}").SendAsync("TimeExtended", result.NewEndTime);
                }
            }
        }

        // Đặt giá tự động
        public async Task PlaceAutoBid(int auctionId, int userId, string userName, decimal maxAmount)
        {
            // TODO: Implement auto-bidding logic
            await Clients.Caller.SendAsync("AutoBidActivated", new { auctionId, maxAmount });
        }

        // Khi client kết nối
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        // Khi client ngắt kết nối
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}