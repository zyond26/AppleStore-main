using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using WebsiteTMDT.Data;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using WebsiteTMDT.ViewModels;
using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace WebsiteTMDT.Controllers
{
    public class AccountController : Controller
    {
        private readonly WebsiteContext _context;

        public AccountController(WebsiteContext context)
        {
            _context = context;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                ViewBag.Error = "Email hoặc mật khẩu không đúng!";
                return View();
            }

            if (user.IsLocked)
            {
                ViewBag.Error = "Tài khoản của bạn đã bị khóa!";
                return View();
            }

            string roleName = user.RoleId switch
            {
                1 => "Admin",
                2 => "Customer",
                _ => "Customer"
            };

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName ?? "User"),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, roleName)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(30)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
            TempData["LoginSuccess"] = "Đăng nhập thành công!";
            return RedirectToAction("Index", "Home");
        }

        // Xử lý đăng nhập bằng Google/Facebook
        public IActionResult ExternalLogin(string provider)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account");
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, provider);
        }

        public async Task<IActionResult> ExternalLoginCallback()
        {
            var info = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (info?.Principal == null)
            {
                return RedirectToAction("Login");
            }

            var claims = info.Principal.Identities.FirstOrDefault()?.Claims;
            var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

            if (email == null)
            {
                return RedirectToAction("Login");
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                user = new User
                {
                    FullName = name,
                    Email = email,
                    PasswordHash = "GoogleOAuth",
                    Phone = "Chưa cập nhật",
                    Address = "Chưa cập nhật",
                    RoleId = 2,
                    CreatedAt = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var role = _context.Roles.FirstOrDefault(r => r.RoleId == user.RoleId);
            string roleName = role?.RoleName ?? "Customer";

            var userClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName ?? "User"),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.RoleId == 1 ? "Admin" : "Customer")
            };

            var identity = new ClaimsIdentity(userClaims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            TempData["LoginGGSuccess"] = "Đăng nhập thành công!";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["LogoutSuccess"] = "Đăng xuất thành công!";
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(string fullName, string email, string password, string phone, string address)
        {
            // Kiểm tra email đã tồn tại
            if (_context.Users.Any(u => u.Email == email))
            {
                ViewBag.Error = "Email đã được sử dụng!";
                return View();
            }

            // Ràng buộc họ tên: không được để trống và ít nhất 2 ký tự
            if (string.IsNullOrWhiteSpace(fullName) || fullName.Length < 2)
            {
                ViewBag.Error = "Họ tên không được để trống và phải từ 2 ký tự trở lên!";
                return View();
            }

            // Ràng buộc địa chỉ: không được để trống
            if (string.IsNullOrWhiteSpace(address))
            {
                ViewBag.Error = "Địa chỉ không được để trống!";
                return View();
            }

            // Ràng buộc email đúng định dạng
            var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (string.IsNullOrWhiteSpace(email) || !System.Text.RegularExpressions.Regex.IsMatch(email, emailPattern))
            {
                ViewBag.Error = "Email không hợp lệ!";
                return View();
            }

            // Ràng buộc mật khẩu: tối thiểu 6 ký tự, bao gồm cả chữ và số
            if (string.IsNullOrEmpty(password) || password.Length < 6 ||
                !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            {
                ViewBag.Error = "Mật khẩu phải có ít nhất 6 ký tự và bao gồm cả chữ và số!";
                return View();
            }

            // Ràng buộc số điện thoại: đúng 10 chữ số
            if (string.IsNullOrEmpty(phone) || !phone.All(char.IsDigit) || phone.Length != 10)
            {
                ViewBag.Error = "Số điện thoại phải gồm đúng 10 chữ số!";
                return View();
            }

            // Mã hóa mật khẩu
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var newUser = new User
            {
                FullName = fullName,
                Email = email,
                PasswordHash = passwordHash,
                Phone = phone,
                Address = address,
                CreatedAt = DateTime.Now,
                RoleId = 2
            };

            _context.Users.Add(newUser);
            _context.SaveChanges();

            return RedirectToAction("Login");
        }

        [Authorize]
        public IActionResult Profile()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // Hiển thị form chỉnh sửa thông tin người dùng
        [Authorize]
        public IActionResult EditProfile()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        [HttpPost]
        public IActionResult EditProfile(string fullName, string email, string phone, string address, string? newPassword)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return NotFound();
            }

            // Cập nhật thông tin
            user.FullName = fullName;
            user.Email = email;
            user.Phone = phone;
            user.Address = address;

            // Nếu người dùng nhập mật khẩu mới, băm mật khẩu và cập nhật
            if (!string.IsNullOrEmpty(newPassword))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            }

            _context.Users.Update(user);
            _context.SaveChanges();

            ViewBag.Success = "Cập nhật thông tin thành công!";
            //return View(user);
            return RedirectToAction("Index", "Home");
        }

        [Authorize(Roles = "Admin")]
        public IActionResult LockAccount(int userId)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return NotFound();
            }

            user.IsLocked = true;
            _context.Users.Update(user);
            _context.SaveChanges();

            TempData["LockSuccess"] = "Tài khoản đã bị khóa thành công!";
            return RedirectToAction("Index", "Users");
        }

        [Authorize(Roles = "Admin")]
        public IActionResult UnlockAccount(int userId)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return NotFound();
            }

            user.IsLocked = false;
            _context.Users.Update(user);
            _context.SaveChanges();

            TempData["UnLockSuccess"] = "Tài khoản đã được mở khóa thành công!";
            return RedirectToAction("Index", "Users");
        }

        [HttpPost]
        public IActionResult ForgotPassword([FromBody] ForgotPasswordModel model)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);
            if (user == null)
            {
                return Json(new { success = false, message = "Email không tồn tại, vui lòng nhập lại." });
            }

            // Tạo link reset password (giả sử bạn tạo theo UserId và token)
            var token = Guid.NewGuid().ToString(); // hoặc Random + Hash
            user.ResetPasswordToken = token;
            user.ResetPasswordExpiry = DateTime.Now.AddHours(1); // Hết hạn sau 1 giờ
            _context.SaveChanges();

            string resetLink = Url.Action("ResetPassword", "Account", new { token = token }, Request.Scheme);
            // Ví dụ ra link: https://yourdomain.com/Account/ResetPassword?token=abcdxyz
            SendResetEmail(user.Email, resetLink);

            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult ResetPassword(string token)
        {
            var user = _context.Users.FirstOrDefault(u => u.ResetPasswordToken == token && u.ResetPasswordExpiry > DateTime.Now);
            if (user == null)
            {
                return Content("Liên kết không hợp lệ hoặc đã hết hạn.");
            }

            // Hiển thị form nhập mật khẩu mới
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public IActionResult ResetPasswordConfirm(string token, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp.";
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.ResetPasswordToken == token && u.ResetPasswordExpiry > DateTime.Now);
            if (user == null)
            {
                return Content("Liên kết không hợp lệ hoặc đã hết hạn.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.ResetPasswordToken = null; 
            user.ResetPasswordExpiry = null;
            _context.SaveChanges();

            return RedirectToAction("Login");
        }

       private void SendResetEmail(string toEmail, string resetLink)
        {
            string fromEmail = Environment.GetEnvironmentVariable("SMTP_EMAIL_ADDRESS");
            string password = Environment.GetEnvironmentVariable("SMTP_EMAIL_PASSWORD");
        
            if (string.IsNullOrEmpty(fromEmail) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("Email hoặc mật khẩu email chưa được cấu hình đúng biến môi trường.");
                return;
            }
        
            string subject = "Đặt lại mật khẩu";
            string body = $@"
                <p>Bạn vừa yêu cầu đặt lại mật khẩu.</p>
                <p>Nhấn vào <a href='{resetLink}'>liên kết này</a> để đặt lại mật khẩu.</p>
                <p>Liên kết sẽ hết hạn sau 1 giờ.</p>";
        
            using (var smtpClient = new SmtpClient("smtp.gmail.com"))
            {
                smtpClient.Port = 587;
                smtpClient.Credentials = new NetworkCredential(fromEmail, password);
                smtpClient.EnableSsl = true;
        
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, "Apple Store"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(toEmail);
        
                try
                {
                    smtpClient.Send(mailMessage);
                    Console.WriteLine("Email đặt lại mật khẩu đã được gửi thành công.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Lỗi gửi email: " + ex.Message);
                }
            }
        }
    }
}
