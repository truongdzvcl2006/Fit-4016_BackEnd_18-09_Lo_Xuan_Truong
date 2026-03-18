// Controllers/AccountController.cs
using Microsoft.AspNetCore.Mvc;
using RealEstateAuction.Data;
using RealEstateAuction.Models;
using System.Linq;
using System;

namespace RealEstateAuction.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            try
            {
                Console.WriteLine($"========== ĐANG XỬ LÝ ĐĂNG NHẬP ==========");
                Console.WriteLine($"Email: {email}");
                Console.WriteLine($"Password: {password}");

                // Kiểm tra đầu vào
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    ViewBag.Error = "Vui lòng nhập email và mật khẩu!";
                    return View("Login");
                }

                // Tìm user trong database
                var user = _context.Users.FirstOrDefault(u => u.Email == email);

                if (user == null)
                {
                    Console.WriteLine("Không tìm thấy user");
                    ViewBag.Error = "Email hoặc mật khẩu không đúng!";
                    return View("Login");
                }

                Console.WriteLine($"Tìm thấy user: {user.Email}, Role: {user.RoleId}");

                // Kiểm tra mật khẩu
                if (user.Password != password)
                {
                    Console.WriteLine("Sai mật khẩu");
                    ViewBag.Error = "Email hoặc mật khẩu không đúng!";
                    return View("Login");
                }

                // Kiểm tra tài khoản có bị khóa không
                if (!user.IsActive)
                {
                    Console.WriteLine("Tài khoản bị khóa");
                    ViewBag.Error = "Tài khoản của bạn đã bị khóa!";
                    return View("Login");
                }

                // Cập nhật thời gian đăng nhập cuối
                user.LastLoginAt = DateTime.Now;
                _context.SaveChanges();

                // Lưu thông tin vào Session
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("UserEmail", user.Email);
                HttpContext.Session.SetInt32("UserRoleId", user.RoleId);
                HttpContext.Session.SetString("UserName", user.FullName);

                Console.WriteLine($"Đăng nhập thành công! UserId: {user.Id}, Role: {user.RoleId}");

                // Chuyển hướng dựa vào role
                if (user.RoleId == 1)
                {
                    Console.WriteLine("Chuyển hướng đến Admin/Index");
                    return RedirectToAction("Index", "Admin");
                }
                else
                {
                    Console.WriteLine("Chuyển hướng đến Home/Index");
                    return RedirectToAction("Index", "Home");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"========== LỖI ĐĂNG NHẬP ==========");
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner: {ex.InnerException.Message}");
                }

                ViewBag.Error = "Có lỗi xảy ra: " + ex.Message;
                return View("Login");
            }
        }

        // GET: /Account/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        // POST: /Account/Register
        [HttpPost]
        public IActionResult Register(string fullName, string email, string password, string phone)
        {
            try
            {
                if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    ViewBag.Error = "Vui lòng điền đầy đủ thông tin!";
                    return View("Login");
                }

                // Kiểm tra email đã tồn tại
                var existingUser = _context.Users.FirstOrDefault(u => u.Email == email);
                if (existingUser != null)
                {
                    ViewBag.Error = "Email đã được sử dụng!";
                    return View("Login");
                }

                // Tạo user mới
                var newUser = new User
                {
                    FullName = fullName,
                    Email = email,
                    Password = password,
                    Phone = phone,
                    RoleId = 2,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                _context.Users.Add(newUser);
                _context.SaveChanges();

                ViewBag.Success = "Đăng ký thành công! Vui lòng đăng nhập.";
                return View("Login");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi đăng ký: {ex.Message}");
                ViewBag.Error = "Có lỗi xảy ra khi đăng ký!";
                return View("Login");
            }
        }
        // GET: /Account/Profile
        [HttpGet]
        public IActionResult Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var user = _context.Users.Find(userId);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            return View(user);
        }

        // POST: /Account/UpdateProfile
        [HttpPost]
        public IActionResult UpdateProfile(int id, string fullName, string phone, string address)
        {
            var user = _context.Users.Find(id);
            if (user == null)
            {
                return NotFound();
            }

            user.FullName = fullName;
            user.Phone = phone;
            user.Address = address;

            _context.SaveChanges();

            // Cập nhật session
            HttpContext.Session.SetString("UserName", user.FullName);

            TempData["Success"] = "Cap nhat thanh cong!";
            return RedirectToAction("Profile");
        }

        // POST: /Account/ChangePassword
        [HttpPost]
        public IActionResult ChangePassword(int id, string currentPassword, string newPassword, string confirmPassword)
        {
            var user = _context.Users.Find(id);
            if (user == null)
            {
                return NotFound();
            }

            // Kiểm tra mật khẩu cũ
            if (user.Password != currentPassword)
            {
                TempData["Error"] = "Mat khau cu khong dung!";
                return RedirectToAction("Profile");
            }

            // Kiểm tra mật khẩu mới và xác nhận
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Mat khau xac nhan khong khop!";
                return RedirectToAction("Profile");
            }

            // Cập nhật mật khẩu mới
            user.Password = newPassword;
            _context.SaveChanges();

            TempData["Success"] = "Doi mat khau thanh cong!";
            return RedirectToAction("Profile");
        }
    }
}