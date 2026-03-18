// Controllers/AuctionController.cs
using Microsoft.AspNetCore.Mvc;
using RealEstateAuction.Services;
using System.Threading.Tasks;

namespace RealEstateAuction.Controllers
{
    public class AuctionController : Controller
    {
        private readonly BiddingEngine _biddingEngine;

        public AuctionController(BiddingEngine biddingEngine)
        {
            _biddingEngine = biddingEngine;
        }

        // GET: /Auction/Index
        public async Task<IActionResult> Index(int id = 1)
        {
            // Kiểm tra session đăng nhập
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Kiểm tra phiên đấu giá đã được khởi tạo chưa
            var session = _biddingEngine.GetAuctionInfo(id);

            if (session == null)
            {
                // Tạo mới phiên đấu giá mẫu
                var config = new BiddingEngine.AuctionConfig
                {
                    StartingPrice = 1000000000, // 1 tỷ
                    FloorPrice = 900000000,      // Giá sàn 900tr
                    CeilingPrice = 2000000000,   // Giá trần 2 tỷ
                    MinBidStep = 10000000,       // Bước giá 10tr
                    MaxBidStep = 100000000,      // Bước giá tối đa 100tr
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now.AddMinutes(30),
                    AutoExtendSeconds = 60
                };

                await _biddingEngine.InitializeAuction(id, "Căn hộ cao cấp Vinhomes", config);
            }

            return View();
        }
    }
}