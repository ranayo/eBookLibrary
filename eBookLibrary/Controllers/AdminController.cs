using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;
using System.Collections.Generic;
using System.Data.Entity;
using eBookLibrary.Models;

namespace eBookLibrary.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Constructor to initialize the database context
        public AdminController()
        {
            _context = new ApplicationDbContext();
        }

        // Admin Dashboard
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        // Manage Users Page
        [HttpGet]
        public ActionResult ManageUsers()
        {
            var users = _context.Users.ToList();
            return View(users);
        }

        // Edit User (GET)
        [HttpGet]
        public ActionResult EditUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }
            return View(user);
        }

        // Edit User (POST)
        [HttpPost]
        public ActionResult EditUser(int id, string username, string email, string newPassword)
        {
            var user = _context.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            user.Username = username;
            user.Email = email;

            if (!string.IsNullOrEmpty(newPassword))
            {
                user.PasswordHash = HashPassword(newPassword);
            }

            _context.SaveChanges();
            return RedirectToAction("ManageUsers");
        }

        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        // Delete User
        [HttpGet]
        public ActionResult DeleteUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }
            return View(user);
        }

        [HttpPost, ActionName("DeleteUser")]
        public ActionResult ConfirmDeleteUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            _context.Users.Remove(user);
            _context.SaveChanges();
            return RedirectToAction("ManageUsers");
        }

        // Add Book (GET)
        [HttpGet]
        public ActionResult AddBook()
        {
            return View();
        }

        [HttpPost]
        public ActionResult AddBook(Book book, HttpPostedFileBase CoverImage)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    if (CoverImage != null && CoverImage.ContentLength > 0)
                    {
                        var fileName = Path.GetFileName(CoverImage.FileName);
                        var filePath = Path.Combine(Server.MapPath("~/Content/Images/Covers"), fileName);
                        Directory.CreateDirectory(Server.MapPath("~/Content/Images/Covers"));
                        CoverImage.SaveAs(filePath);
                        book.CoverImagePath = fileName;
                    }

                    _context.Books.Add(book);
                    _context.SaveChanges();
                    TempData["Message"] = "Book added successfully!";
                    return RedirectToAction("ManageCatalog");
                }

                TempData["Error"] = "Please fill in all required fields.";
                return View(book);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while adding the book: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Manage Catalog
        [HttpGet]
        public ActionResult ManageCatalog()
        {
            var books = _context.Books.ToList();
            return View(books);
        }

        [HttpPost]
        public ActionResult DeleteBook(int id)
        {
            try
            {
                var book = _context.Books.SingleOrDefault(b => b.Id == id);
                if (book == null)
                {
                    TempData["Error"] = "Book not found.";
                    return RedirectToAction("ManageCatalog");
                }

                _context.Books.Remove(book);
                _context.SaveChanges();
                TempData["Message"] = "Book deleted successfully!";
                return RedirectToAction("ManageCatalog");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while deleting the book: {ex.Message}";
                return RedirectToAction("ManageCatalog");
            }
        }

        [HttpGet]
        public ActionResult EditBook(int id)
        {
            var book = _context.Books.SingleOrDefault(b => b.Id == id);
            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("ManageCatalog");
            }

            return View(book);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditBook(Book book, HttpPostedFileBase CoverImage)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the errors in the form.";
                return View(book);
            }

            var bookInDb = _context.Books.SingleOrDefault(b => b.Id == book.Id);
            if (bookInDb == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("ManageCatalog");
            }

            bookInDb.Title = book.Title;
            bookInDb.Author = book.Author;
            bookInDb.Publisher = book.Publisher;
            bookInDb.Genre = book.Genre;
            bookInDb.BuyPrice = book.BuyPrice;
            bookInDb.BorrowPrice = book.BorrowPrice;
            bookInDb.IsAvailableForBorrow = book.IsAvailableForBorrow;
            bookInDb.CopiesAvailable = book.CopiesAvailable;
            bookInDb.YearOfPublishing = book.YearOfPublishing;
            bookInDb.AgeLimit = book.AgeLimit;
            bookInDb.DiscountPrice = book.DiscountPrice;
            bookInDb.DiscountStartDate = book.DiscountStartDate;
            bookInDb.DiscountEndDate = book.DiscountEndDate;
            bookInDb.InStock = book.InStock;
            bookInDb.IsBuyOnly = book.IsBuyOnly;

            if (CoverImage != null && CoverImage.ContentLength > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(CoverImage.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("CoverImage", "Only image files are allowed.");
                    return View(book);
                }

                int maxFileSize = 2 * 1024 * 1024;
                if (CoverImage.ContentLength > maxFileSize)
                {
                    ModelState.AddModelError("CoverImage", "The cover image size cannot exceed 2 MB.");
                    return View(book);
                }

                var fileName = Path.GetFileName(CoverImage.FileName);
                var uploadsFolder = Server.MapPath("~/Uploads");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
                var path = Path.Combine(uploadsFolder, uniqueFileName);

                try
                {
                    CoverImage.SaveAs(path);
                    bookInDb.CoverImagePath = "/Uploads/" + uniqueFileName;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error uploading the cover image.");
                    return View(book);
                }
            }

            try
            {
                _context.SaveChanges();
                TempData["Message"] = "Book updated successfully!";
                return RedirectToAction("ManageCatalog");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while updating the book.";
                return View(book);
            }
        }

        // Notifications for Waiting List
        public void NotifyUser(int bookId)
        {
            try
            {
                var waitingList = _context.WaitingLists.Where(w => w.BookId == bookId).OrderBy(w => w.DateAdded).ToList();
                if (waitingList.Any())
                {
                    var nextUser = waitingList.First();
                    TempData["Message"] = $"User {nextUser.UserId} has been notified about the availability of the book.";
                    _context.WaitingLists.Remove(nextUser);
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while notifying the user: {ex.Message}";
            }
        }

        // Reminder for Borrowed Books
        public void SendReminder()
        {
            try
            {
                var borrowedBooks = _context.BorrowedBooks.Where(b => b.ReturnDate <= DateTime.Now.AddDays(5)).ToList();
                foreach (var borrowedBook in borrowedBooks)
                {
                    TempData["Message"] = $"Reminder sent to User {borrowedBook.UserId} for book {borrowedBook.BookId}.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while sending reminders: {ex.Message}";
            }
        }

        // Dispose resources
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
