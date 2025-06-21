using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebsiteTMDT.Data;
using WebsiteTMDT.Models;
using WebsiteTMDT.ViewModels;

namespace WebsiteTMDT.Controllers
{
    public class ProductController : Controller
    {
        private readonly WebsiteContext db;

        public ProductController(WebsiteContext context)
        {
            db = context;
        }

        public async Task<IActionResult> Index(int? loai, int page = 1, int pageSize = 6, string sortBy = "popular")
        {
            var products = db.Products.AsQueryable();

            // Lọc theo danh mục (nếu có)
            if (loai.HasValue)
            {
                products = products.Where(p => p.CategoryId == loai.Value);
            }

            // Sắp xếp theo tiêu chí được chọn
            switch (sortBy)
            {
                case "price_asc":
                    products = products.OrderBy(p => p.Price);
                    break;
                case "price_desc":
                    products = products.OrderByDescending(p => p.Price);
                    break;
                default:
                    // Mặc định sắp xếp theo ID (Popular)
                    products = products.OrderBy(p => p.ProductId);
                    break;
            }

            // Tính tổng số sản phẩm để phân trang
            int totalItems = await products.CountAsync();

            // Áp dụng phân trang
            var paginatedProducts = await products
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductsVM
                {
                    MaSP = p.ProductId,
                    TenSP = p.ProductName,
                    HinhAnh = p.ImageUrl,
                    Gia = (double)p.Price,
                    MoTa = p.Description,
                    TenLoai = p.Category.CategoryName
                }).ToListAsync();

            // Truyền dữ liệu sang View
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.Loai = loai;
            ViewBag.SortBy = sortBy;
            ViewBag.PageSize = pageSize;

            return View(paginatedProducts);
        }

        public IActionResult Search(string? query)
        {
            var products = db.Products.AsQueryable();
            if (query != null)
            {
                products = products.Where(p => p.ProductName.Contains(query));
            }

            var result = products.Select(p => new ProductsVM
            {
                MaSP = p.ProductId,
                TenSP = p.ProductName,
                HinhAnh = p.ImageUrl,
                Gia = (double)p.Price,
                MoTa = p.Description,
                TenLoai = p.Category.CategoryName
            });
            return View(result);
        }

        public IActionResult Detail(int id)
        {
            var data = db.Products
                .Include(p => p.Category)
                .SingleOrDefault(p => p.ProductId == id);

            if (data == null)
            {
                TempData["Message"] = $"Không tìm thấy sản phẩm có mã {id}";
                return RedirectToAction("/404");
            }

            // Lấy 4 sản phẩm cùng loại
            var relatedProducts = db.Products
                .Include(p => p.Category)
                .Where(p => p.ProductId != id && p.CategoryId == data.CategoryId)
                .Take(4)
                .ToList();

            // Lấy danh sách Review
            var reviews = db.Reviews
       .Where(r => r.ProductId == id)
       .Select(r => new ReviewVM
       {
           tenKhachHang = r.User.FullName,
           danhGia = r.Rating,
           ngayDang = r.CreatedAt,
           noiDung = r.ReviewText
       })
       .ToList();

            bool daMuaHang = false;
            bool choPhepDanhGia = false;

            try
            {
                int userId = GetUserId();
                int soLanMua = db.OrderDetails
                    .Include(od => od.Order)
                    .Where(od => od.ProductId == id
                              && od.Order.UserId == userId
                              && od.Order.Status.Trim().ToLower() == "completed")
                    .Count();

                int soLanDanhGia = db.Reviews
                    .Where(r => r.ProductId == id && r.UserId == userId)
                    .Count();

                // Kiểm tra nếu đã mua ít nhất một lần
                if (soLanMua > 0)
                {
                    daMuaHang = true;

                    // Nếu mua thêm và chưa đánh giá sản phẩm lần nào
                    if (soLanMua > soLanDanhGia)
                    {
                        choPhepDanhGia = true;
                    }
                }
            }
            catch
            {
                daMuaHang = false;
                choPhepDanhGia = false;
            }

            var result = new ProductsDetailVM
            {
                MaLoai = data.CategoryId,
                MaSP = data.ProductId,
                TenSP = data.ProductName,
                HinhAnh = data.ImageUrl,
                Gia = (double)data.Price,
                MoTa = data.Description,
                TenLoai = data.Category.CategoryName,
                ChiTiet = data.Description ?? string.Empty,
                DiemDanhGia = 5,
                SoLuongTon = data.StockQuantity,
                MauSac = data.Color,
                DungLuong = data.Capacity,
                SoLuong = data.StockQuantity,
                Products = relatedProducts,
                Reviews = reviews,
                DaMuaHang = daMuaHang,
                ChoPhepDanhGia = choPhepDanhGia
            };

            return View(result);
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        }
        
    }
}
