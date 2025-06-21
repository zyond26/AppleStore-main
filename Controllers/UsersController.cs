using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebsiteTMDT.Data;

namespace WebsiteTMDT.Controllers
{
    public class UsersController : Controller
    {
        private readonly WebsiteContext _context;

        public UsersController(WebsiteContext context)
        {
            _context = context;
        }

        // GET: Users
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(string searchTerm, string sortOrder, DateTime? createdDate, DateTime? lastOrderDate, int pageNumber = 1, int pageSize = 10)
        {
            var usersQuery = _context.Users.Include(u => u.Role).AsQueryable();

            // Tìm kiếm theo tên hoặc email
            if (!string.IsNullOrEmpty(searchTerm))
            {
                usersQuery = usersQuery.Where(u => u.FullName.Contains(searchTerm) || u.Email.Contains(searchTerm));
            }

            // Lọc theo ngày tạo
            if (createdDate.HasValue)
            {
                usersQuery = usersQuery.Where(u => u.CreatedAt.Value.Date == createdDate.Value.Date);
            }

            // Lọc theo ngày đặt hàng cuối cùng
            if (lastOrderDate.HasValue)
            {
                usersQuery = usersQuery.Where(u => u.Orders.Any(o => o.CreatedAt.Value.Date == lastOrderDate.Value.Date));
            }

            // Sắp xếp theo ngày tạo (mặc định mới nhất lên đầu)
            usersQuery = usersQuery.OrderByDescending(u => u.CreatedAt);

            // Phân trang
            int totalUsers = await usersQuery.CountAsync();
            var users = await usersQuery.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

            // Truyền dữ liệu sang View
            ViewBag.SearchTerm = searchTerm;
            ViewBag.CreatedDate = createdDate?.ToString("yyyy-MM-dd");
            ViewBag.LastOrderDate = lastOrderDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = pageNumber;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);

            return View(users);
        }

        // GET: Users/Details/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(m => m.UserId == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: Users/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "RoleName");
            TempData["CreateUser"] = "Thêm người dùng thành công!";
            return View();
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,Email,PasswordHash,Phone,Address,RoleId,IsLocked")] User user)
        {
            if (ModelState.IsValid)
            {
                // Hash mật khẩu nếu có nhập
                if (!string.IsNullOrEmpty(user.PasswordHash))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash, 10);
                }

                user.CreatedAt = DateTime.Now; // Gán thời gian tạo

                _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "RoleName", user.RoleId);
            return View(user);
        }

        // GET: UserManage/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "RoleName", user.RoleId);
            TempData["EditUser"] = "Cập nhật thông tin người dùng thành công!";
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("UserId,FullName,Email,PasswordHash,Phone,Address,CreatedAt,RoleId,IsLocked")] User user)
        {
            if (id != user.UserId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == id);
                    if (existingUser == null)
                    {
                        return NotFound();
                    }

                    // Nếu IsLocked là null, gán giá trị mặc định là false
                    if (user.IsLocked == null)
                    {
                        user.IsLocked = false;
                    }

                    if (string.IsNullOrEmpty(user.PasswordHash))
                    {
                        user.PasswordHash = existingUser.PasswordHash;
                    }
                    else
                    {
                        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash, 10);
                    }

                    _context.Update(user);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.UserId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["RoleId"] = new SelectList(_context.Roles, "RoleId", "RoleName", user.RoleId);
            return View(user);
        }

        // GET: Users/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(m => m.UserId == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.UserId == id);
        }
    }
}
