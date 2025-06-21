﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using WebsiteTMDT.Data;

namespace WebsiteTMDT.Controllers
{
    [Authorize] 
    public class OrderController : Controller
    {
        private readonly WebsiteContext _context;

        public OrderController(WebsiteContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Index(string searchTerm, string status, DateTime? createdDate, int pageNumber = 1, int pageSize = 10)
        {
            var query = _context.Orders.Include(o => o.User).AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(o => o.OrderId.ToString().Contains(searchTerm) || o.User.FullName.Contains(searchTerm));
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
            }

            if (createdDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt.Value.Date == createdDate.Value.Date);
            }

            // SẮP XẾP: ngày đặt mới nhất trước
            query = query.OrderByDescending(o => o.CreatedAt);

            int totalItems = query.Count();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var orders = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.Status = status;
            ViewBag.CreatedDate = createdDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;

            return View(orders);
        }

        [HttpGet("details/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        [HttpPost("update-status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(int orderId, string status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return NotFound();
            }

            // Danh sách trạng thái theo thứ tự
            var statusOrder = new List<string> { "Pending", "Processing", "Shipped", "Completed", "Cancelled" };

            // Kiểm tra status hợp lệ
            if (string.IsNullOrEmpty(status) || !statusOrder.Contains(status))
            {
                TempData["Error"] = "Trạng thái đơn hàng không hợp lệ!";
                return RedirectToAction("Index");
            }

            int currentStatusIndex = statusOrder.IndexOf(order.Status);
            int newStatusIndex = statusOrder.IndexOf(status);

            // ✅ Chỉ cho phép hủy nếu đơn hàng đang ở trạng thái Pending
            if (status == "Cancelled")
            {
                if (order.Status == "Pending")
                {
                    order.Status = status;
                }
                else
                {
                    TempData["Error"] = "Chỉ được hủy đơn hàng khi đang ở trạng thái Pending!";
                    return RedirectToAction("Index");
                }
            }
            // ✅ Bắt buộc cập nhật đúng thứ tự (chỉ +1)
            else if (newStatusIndex == currentStatusIndex + 1)
            {
                order.Status = status;
            }
            else
            {
                TempData["Error"] = "Bạn chỉ được phép cập nhật theo đúng trình tự!";
                return RedirectToAction("Index");
            }

            order.UpdatedAt = DateTime.UtcNow;
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            TempData["UpdateSuccess"] = "Cập nhật trạng thái đơn hàng thành công!";
            return RedirectToAction("Index");
        }

        // Danh sách đơn hàng của khách hàng
        [HttpGet("order-history")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> OrderHistory()
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return Unauthorized();
            }

            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .Include(o => o.OrderDetails)
                .ToListAsync();

            return View(orders);
        }

        [HttpGet("OrderDetails/{orderId}")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> OrderDetails(int orderId)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return Unauthorized();
            }

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAdminNotifications()
        {
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);

            // Xóa các thông báo cũ hơn 30 ngày
            var oldNotifications = await _context.Notifications
                .Where(n => n.CreatedAt < thirtyDaysAgo)
                .ToListAsync();

            if (oldNotifications.Any())
            {
                _context.Notifications.RemoveRange(oldNotifications);
                await _context.SaveChangesAsync();
            }

            // Lấy các thông báo mới trong 30 ngày gần nhất
            var notifications = await _context.Notifications
                .Where(n => n.CreatedAt >= thirtyDaysAgo)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    message = n.Content,
                    createdAt = n.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            return Json(new
            {
                count = notifications.Count,
                data = notifications
            });
        }

        [Authorize(Roles = "Customer")]
        [HttpPost]
        public IActionResult CancelOrder(int orderId)
        {
            var order = _context.Orders.SingleOrDefault(o => o.OrderId == orderId);

            if (order == null)
            {
                TempData["CancelError"] = "Đơn hàng không tồn tại!";
                return RedirectToAction("Index");
            }

            if (order.Status != "Pending")
            {
                TempData["CancelError"] = "Không thể hủy đơn hàng này. Đơn hàng đã được xử lý.";
                return RedirectToAction("Index");
            }

            // Hủy đơn hàng
            order.Status = "Cancelled"; 
            _context.SaveChanges();

            TempData["CancelSuccess"] = "Đơn hàng đã được hủy thành công!";
            return RedirectToAction("OrderDetails");
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
        }
    }
}
