using Microsoft.EntityFrameworkCore;
using RealEstateAuction.Data;
using RealEstateAuction.Services;
using RealEstateAuction.Hubs; // Thêm namespace cho Hub

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Cấu hình DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));

// Đăng ký Services
builder.Services.AddScoped<IPropertyService, PropertyService>();

// Đăng ký Bidding Engine (Singleton - dùng chung cho toàn bộ ứng dụng)
builder.Services.AddSingleton<BiddingEngine>();

// Đăng ký SignalR
builder.Services.AddSignalR();

// Cấu hình Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session tồn tại 30 phút
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

// Map controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map SignalR Hub - Đây là phần QUAN TRỌNG để SignalR hoạt động
app.MapHub<AuctionHub>("/auctionHub");

app.Run();