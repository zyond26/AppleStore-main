﻿using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebsiteTMDT.Data;
using WebsiteTMDT.Models;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace WebsiteTMDT
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Thêm kết nối Database vào DI
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<WebsiteContext>(options =>
                options.UseSqlServer(connectionString));

            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
              .AddCookie(options =>
              {
                  var config = builder.Configuration.GetSection("Authentication:Cookie");
                  options.LoginPath = config["LoginPath"];
                  options.LogoutPath = config["LogoutPath"];
                  options.AccessDeniedPath = config["AccessDeniedPath"];
                  options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Duy trì đăng nhập trong 30 phút
                  options.SlidingExpiration = true; // Gia hạn phiên nếu có hoạt động
              })
              .AddGoogle(options =>
                {
                    options.ClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
                    options.ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
                    options.CallbackPath = "/signin-google";
                })
                .AddFacebook(options =>
                {
                    options.AppId = Environment.GetEnvironmentVariable("FACEBOOK_APP_ID");
                    options.AppSecret = Environment.GetEnvironmentVariable("FACEBOOK_APP_SECRET");
                    options.CallbackPath = "/signin-facebook";
                });

            // Cấu hình Authorization
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
                options.AddPolicy("CustomerPolicy", policy => policy.RequireRole("Customer"));
            });

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            builder.Services.Configure<RequestLocalizationOptions>(options =>
            {
                // Tạo danh sách các CultureInfo từ mảng string
                var supportedCultures = new[] { "en-US", "en-GB" }
                    .Select(culture => new CultureInfo(culture))
                    .ToList();

                options.DefaultRequestCulture = new RequestCulture("en-US");
                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<WebsiteContext>();

                var users = context.Users.Where(u => !u.PasswordHash.StartsWith("$2a$")).ToList();
                foreach (var user in users)
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash, 10);
                }

                if (users.Any())
                {
                    context.SaveChanges();
                    Console.WriteLine("✅ Đã cập nhật mật khẩu cũ thành mã hóa BCrypt.");
                }
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            // Cấu hình Localization Middleware
            var localizationOptions = new RequestLocalizationOptions()
                .SetDefaultCulture("en-US")
                .AddSupportedCultures("en-US", "en-GB")
                .AddSupportedUICultures("en-US", "en-GB");

            app.UseRequestLocalization(localizationOptions);
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseCors("AllowAll");
            app.UseAuthentication(); 
            app.UseAuthorization();
            app.UseSession();


            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            //app.UseEndpoints(endpoints =>
            //{
            //    endpoints.MapControllerRoute(
            //        name: "default",
            //        pattern: "{controller=Report}/{action=Index}/{id?}");
            //});

            app.Run();
        }
    }
}
