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
    public class ProductManageController : Controller
    {
        private readonly WebsiteContext _context;

        public ProductManageController(WebsiteContext context)
        {
            _context = context;
        }

        // GET: ProductManage
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(string searchTerm, int? categoryId, int pageNumber = 1, int pageSize = 10)
        {
            var query = _context.Products.Include(p => p.Category).AsQueryable();

            // Lọc theo tên sản phẩm
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p => p.ProductName.Contains(searchTerm));
            }

            // Lọc theo danh mục sản phẩm
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId);
            }

            // Phân trang
            int totalItems = await query.CountAsync();
            var products = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

            // Truyền dữ liệu cho ViewBag
            ViewBag.SearchTerm = searchTerm;
            ViewBag.CategoryId = categoryId;
            ViewBag.CurrentPage = pageNumber;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.Categories = await _context.Categories.ToListAsync();

            return View(products);
        }

        // GET: ProductManage/Details/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.ProductId == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET: ProductManage/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName");
            TempData["CreateProduct"] = "Thêm sản phẩm thành công!";
            return View();
        }

        // POST: ProductManage/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductId,ProductName,CategoryId,Price,StockQuantity,Capacity,Color,ImageUrl,Description,CreatedAt")] Product product)
        {
            if (ModelState.IsValid)
            {
                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName", product.CategoryId);
            return View(product);
        }

        // GET: ProductManage/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // Đảm bảo ViewBag có dữ liệu
            ViewData["CategoryId"] = new SelectList(_context.Categories.ToList(), "CategoryId", "CategoryName", product.CategoryId);
            TempData["EditProduct"] = "Cập nhật thông tin sản phẩm thành công!";
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ProductId,ProductName,CategoryId,Price,StockQuantity,Capacity,Color,ImageUrl,Description,CreatedAt")] Product product)
        {
            if (id != product.ProductId)
            {
                return NotFound();
            }

            // ✅ Log giá trị nhận được từ form
            Console.WriteLine($"Received CategoryId: {product.CategoryId}");

            if (!ModelState.IsValid)
            {
                // Debug lỗi validation
                foreach (var key in ModelState.Keys)
                {
                    foreach (var error in ModelState[key].Errors)
                    {
                        Console.WriteLine($"Validation Error - {key}: {error.ErrorMessage}");
                    }
                }

                // Đảm bảo ViewBag có danh mục khi reload lại form
                ViewData["CategoryId"] = new SelectList(_context.Categories.ToList(), "CategoryId", "CategoryName", product.CategoryId);
                return View(product);
            }

            try
            {
                _context.Update(product);
                await _context.SaveChangesAsync();
                Console.WriteLine("Lưu dữ liệu thành công!");
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine("Lỗi khi lưu vào database: " + ex.Message);
            }

            ViewData["CategoryId"] = new SelectList(_context.Categories.ToList(), "CategoryId", "CategoryName", product.CategoryId);
            return View(product);
        }

        // GET: ProductManage/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.ProductId == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: ProductManage/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.ProductId == id);
        }
    }
}
