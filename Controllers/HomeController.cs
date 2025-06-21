using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebsiteTMDT.Data;
using WebsiteTMDT.ViewModels;

namespace WebsiteTMDT.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly WebsiteContext db;

        public HomeController(WebsiteContext context)
        {
            db = context;
        }

        public IActionResult Index(int? loai)
        {
            var products = db.Products.AsQueryable();
            if (loai.HasValue)
            {
                products = products.Where(p => p.CategoryId == loai.Value);
            }

            var result = products.Select(p => new ProductsVM
            {
                MaLoai = p.CategoryId,
                MaSP = p.ProductId,
                TenSP = p.ProductName,
                HinhAnh = p.ImageUrl,
                Gia = (double)p.Price,
                MoTa = p.Description,
                TenLoai = p.Category.CategoryName
            });
            return View(result);
        }

        [Route("/404")]
        public IActionResult PageNotFound()
        {
            return View();
        }

        public IActionResult HotDeal()
        {
            var products = db.Products.Select(p => new ProductsVM
            {
                MaSP = p.ProductId, 
                MaLoai = (int)p.CategoryId,
                TenSP = p.ProductName,
                Gia = (double)p.Price,
                HinhAnh = p.ImageUrl,
                TenLoai = p.Category.CategoryName
            }).ToList();

            return View(products);
        }

        public IActionResult Contact()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForm(ChatMessage model)
        {
            if (ModelState.IsValid)
            {
                // Create new ChatMessage object
                var chatMessage = new ChatMessage
                {
                    Name = model.Name,
                    Email = model.Email,
                    Message = model.Message,
                    CreatedAt = DateTime.UtcNow
                };

                // Save to the database
                db.ChatMessages.Add(chatMessage);
                await db.SaveChangesAsync();

                // Optionally, you can send a confirmation message or redirect to another page
                TempData["SuccessMessage"] = "Tin nhắn đã được gửi thành công!";
                return RedirectToAction("Contact");  // Or redirect to another view
            }

            // Return the same page if there's an error
            return View("Index", model);
        }

        public IActionResult DashBoard()
        {
            return View();
        }
    }
}
