using Microsoft.AspNetCore.Mvc;
using WebsiteTMDT.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Net.Mail;
using System.Net;

namespace WebsiteTMDT.Controllers
{
    public class ChatController : Controller
    {
        private readonly WebsiteContext _context;

        public ChatController(WebsiteContext context)
        {
            _context = context;
        }

        // POST: SaveMessage
        [HttpPost]
        [Authorize(Roles = "Customer")]
        public IActionResult SaveMessage([FromBody] ChatMessage model)
        {
            // Lấy UserId từ claims của người dùng
            var userId = GetUserId();

            if (userId == 0)  // Kiểm tra nếu UserId không hợp lệ
            {
                return Json(new { success = false, message = "Người dùng chưa đăng nhập." });
            }

            // Lấy thông tin người dùng từ cơ sở dữ liệu dựa trên UserId
            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);

            if (user == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            // Tạo một đối tượng ChatMessage với thông tin người dùng và tin nhắn
            var chatMessage = new ChatMessage
            {
                Name = user.FullName,  // Lấy FullName từ bảng Users
                Email = user.Email,    // Lấy Email từ bảng Users
                Message = model.Message,
                CreatedAt = DateTime.Now
            };

            // Thêm tin nhắn vào cơ sở dữ liệu và lưu
            _context.ChatMessages.Add(chatMessage);
            _context.SaveChanges();

            // Trả về kết quả
            return Json(new { success = true, message = "Tin nhắn đã được gửi thành công!" });
        }

        // GET: Message/Index
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var messages = await _context.ChatMessages
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
            return View(messages);
        }

        // GET: Message/Details/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int id)
        {
            var message = await _context.ChatMessages
                .FirstOrDefaultAsync(m => m.Id == id);

            if (message == null)
            {
                return NotFound();
            }

            return View(message);
        }


        // POST: Message/Delete/5
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var message = await _context.ChatMessages.FindAsync(id);
            if (message != null)
            {
                _context.ChatMessages.Remove(message);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Message/Reply/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reply(int id)
        {
            var message = await _context.ChatMessages.FindAsync(id);
            if (message == null)
            {
                return NotFound();
            }

            // Pass the message with the reply status to the view
            return View(message);
        }

        // POST: Message/Reply/5
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(int id, string reply)
        {
            var message = await _context.ChatMessages.FindAsync(id);
            if (message == null)
            {
                return NotFound();
            }

            // Cập nhật tin nhắn với câu trả lời
            message.Reply = reply;
            await _context.SaveChangesAsync();

            // Gửi email trả lời cho khách hàng
            SendReplyToCustomer(message.Email, reply);

            TempData["SuccessMessage"] = "Câu trả lời đã được gửi thành công!";
            return RedirectToAction(nameof(Index));
        }

        // Phương thức gửi email trả lời cho khách hàng
        [Authorize(Roles = "Admin")]
        private void SendReplyToCustomer(string customerEmail, string replyMessage)
        {
            string fromEmail = Environment.GetEnvironmentVariable("SMTP_EMAIL_ADDRESS");
            string password = Environment.GetEnvironmentVariable("SMTP_EMAIL_PASSWORD");

            string subject = "Tin nhắn của bạn đã được trả lời";
            string body = $"<p>Kính gửi khách hàng,</p>" +
                          $"<p>Chúng tôi xin thông báo rằng tin nhắn của bạn đã được trả lời: </p>" +
                          $"<p>{replyMessage}</p>" +
                          "<p>Trân trọng,</p>" +
                          "<p>Đội ngũ hỗ trợ khách hàng</p>";

            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(fromEmail, password),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "Apple Store"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            };
            mailMessage.To.Add(customerEmail);

            try
            {
                smtpClient.Send(mailMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi gửi mail: " + ex.Message);
            }
        }

        // Lấy UserId từ Claims và trả về
        private int GetUserId()
        {
            return int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
        }
    }
}
