using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Net.Mail;
using System.Threading.Tasks;
using eBookLibrary.Models;
using System.Net;

namespace eBookLibrary.Controllers
{
    public class BooksController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BooksController()
        {
            _context = new ApplicationDbContext();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }

        // Display all books with filters
        public ActionResult Index(string search, string sortBy, string genre, int? ageLimit, bool? onSale, int? yearOfPublishing, string availability, int? page)
        {
            int pageSize = 10; // Number of books per page
            int pageNumber = page ?? 1;
            var books = _context.Books.AsQueryable();

            ViewBag.Genres = _context.Books.Select(b => b.Genre).Distinct().ToList();
            ViewBag.Years = _context.Books.Select(b => b.YearOfPublishing).Distinct().OrderByDescending(y => y).ToList();


            // Apply filters
            if (!string.IsNullOrEmpty(search))
            {
                books = books.Where(b => b.Title.Contains(search) || b.Author.Contains(search) || b.Publisher.Contains(search));
            }
            if (!string.IsNullOrEmpty(genre))
            {
                books = books.Where(b => b.Genre == genre);
            }
            if (ageLimit.HasValue)
            {
                books = books.Where(b => b.AgeLimit >= ageLimit.Value);
            }
            if (yearOfPublishing.HasValue)
            {
                books = books.Where(b => b.YearOfPublishing == yearOfPublishing.Value);
            }
            if (onSale.HasValue && onSale.Value)
            {
                books = books.Where(b => b.DiscountPrice.HasValue);
            }
            // Apply availability filter
            if (!string.IsNullOrEmpty(availability))
            {
                if (availability == "borrow")
                {
                    books = books.Where(b => b.IsAvailableForBorrow); // Borrow Only
                }
                else if (availability == "buy")
                {
                    books = books.Where(b => !b.IsAvailableForBorrow); // Buy Only
                }
            }

            // Sorting
            switch (sortBy)
            {
                case "priceAsc":
                    books = books.OrderBy(b => b.DiscountPrice ?? b.BuyPrice);
                    break;
                case "priceDesc":
                    books = books.OrderByDescending(b => b.DiscountPrice ?? b.BuyPrice);
                    break;
                case "popularity":
                    books = books.OrderByDescending(b => b.Popularity);
                    break;
                case "year":
                    books = books.OrderByDescending(b => b.YearOfPublishing);
                    break;
                default:
                    books = books.OrderBy(b => b.Title);
                    break;
            }

            // Paginate results
            var pagedBooks = books.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            ViewBag.TotalPages = (int)Math.Ceiling((double)books.Count() / pageSize);
            ViewBag.CurrentPage = pageNumber;

            return View(pagedBooks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> BuyBookAsync(int bookId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                TempData["Error"] = "You must be logged in to buy a book.";
                return RedirectToAction("Index");
            }

            var book = _context.Books.SingleOrDefault(b => b.Id == bookId);
            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("Index");
            }

            if (book.InStock > 0)
            {
                // Decrement the stock count
                book.InStock--;

                // Record the purchase
                var purchase = new Purchase
                {
                    BookId = bookId,
                    UserId = userId,
                    PurchaseDate = DateTime.Now,
                    Price = book.BuyPrice
                };

                _context.Purchases.Add(purchase);
                _context.SaveChanges();

                // Debugging
                System.Diagnostics.Debug.WriteLine("Book purchase recorded. Calling SendEmailAsync...");

                // Send email notification
                await SendEmailAsync("ranayoun11@gmail.com", "Book Purchase Confirmation", $"You have successfully purchased the book: {book.Title}.");

                System.Diagnostics.Debug.WriteLine("SendEmailAsync completed.");
                TempData["Message"] = "You successfully purchased the book.";
            }
            else
            {
                TempData["Error"] = "This book is out of stock.";
            }

            return RedirectToAction("Index");
        }



        [HttpGet]
        public ActionResult BorrowBook(int bookId)
        {
            // Fetch the selected book
            var book = _context.Books.SingleOrDefault(b => b.Id == bookId);

            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("Index");
            }

            // Calculate waiting list count and next availability
            var waitingList = _context.WaitingLists.Where(w => w.BookId == bookId).OrderBy(w => w.DateAdded).ToList();
            var nextAvailabilityDate = waitingList.Any()
                ? waitingList.First().DateAdded.AddDays(30) // Assuming 30-day borrow period
                : (book.CopiesAvailable > 0 ? DateTime.Now : (DateTime?)null);

            var viewModel = new BorrowViewModel
            {
                Book = book,
                WaitingListCount = waitingList.Count,
                NextAvailabilityDate = nextAvailabilityDate
            };

            return View(viewModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> BorrowBookRequestAsync(int bookId)
        {
            int userId = GetCurrentUserId();
            if (userId == 0)
            {
                TempData["Error"] = "You must be logged in to borrow a book.";
                return RedirectToAction("Index");
            }

            var book = _context.Books.SingleOrDefault(b => b.Id == bookId);
            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("Index");
            }

            if (book.CopiesAvailable > 0)
            {
                // Borrow the book directly
                var borrowedBook = new BorrowedBook
                {
                    BookId = bookId,
                    UserId = userId,
                    BorrowDate = DateTime.Now,
                    ReturnDate = DateTime.Now.AddDays(30) // Default borrowing period: 30 days
                };

                book.CopiesAvailable--;
                _context.BorrowedBooks.Add(borrowedBook);
                _context.SaveChanges();

                // Send email notification
                await SendEmailAsync("ranayoun11@gmail.com", "Book Borrow Confirmation", $"You have successfully borrowed the book: {book.Title}. Please return it by {borrowedBook.ReturnDate.ToShortDateString()}.");

                TempData["Message"] = "You successfully borrowed the book!";
            }
            else
            {
                // Add the user to the waiting list
                return RedirectToAction("AddToWaitingList", new { bookId });
            }

            return RedirectToAction("Index");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReturnBook(int bookId)
        {
            int userId = GetCurrentUserId();

            var borrowedBook = _context.BorrowedBooks.FirstOrDefault(b => b.BookId == bookId && b.UserId == userId);
            if (borrowedBook != null)
            {
                _context.BorrowedBooks.Remove(borrowedBook);

                var book = _context.Books.SingleOrDefault(b => b.Id == bookId);
                if (book != null)
                {
                    book.CopiesAvailable++;

                    // Notify the next three users in the waiting list
                    NotifyNextUsersInWaitingList(bookId).Wait();
                }

                _context.SaveChanges();

                TempData["Message"] = "Book returned successfully.";
            }
            else
            {
                TempData["Error"] = "You have not borrowed this book.";
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult WaitingListView(int bookId)
        {
            // Fetch the selected book
            var book = _context.Books.SingleOrDefault(b => b.Id == bookId);
            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("Index");
            }

            // Fetch waiting list and sort by DateAdded
            var waitingList = _context.WaitingLists
                .Where(w => w.BookId == bookId)
                .OrderBy(w => w.DateAdded)
                .ToList() // Execute the query
                .Select(w =>
                {
            // Fetch the user details
            var user = _context.Users.FirstOrDefault(u => u.Id == w.UserId);

            // Use ExpectedAvailabilityDate directly from the database
            return new WaitingListViewModel
                    {
                        Username = user != null ? user.Username : "Unknown",
                        DateAdded = w.DateAdded,
                        ExpectedAvailabilityDate = w.ExpectedAvailabilityDate // Pull directly from the database
            };
                })
                .ToList();

            // Pass the data to the ViewModel
            var viewModel = new WaitingListPageViewModel
            {
                BookTitle = book.Title,
                WaitingList = waitingList
            };

            return View(viewModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddToWaitingList(int bookId)
        {
            int userId;
            if (Session["UserId"] == null || !int.TryParse(Session["UserId"].ToString(), out userId))
            {
                TempData["Error"] = "You must be logged in to join the waiting list.";
                return RedirectToAction("Index");
            }

            var book = _context.Books.SingleOrDefault(b => b.Id == bookId);
            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("Index");
            }

            var existingEntry = _context.WaitingLists.FirstOrDefault(w => w.BookId == bookId && w.UserId == userId);
            if (existingEntry == null)
            {
                // Each user's ExpectedAvailabilityDate is 30 days from their own DateAdded
                var waitingEntry = new WaitingList
                {
                    BookId = bookId,
                    UserId = userId,
                    DateAdded = DateTime.Now, // Date when the user joins
                    ExpectedAvailabilityDate = DateTime.Now.AddDays(30) // Exactly 30 days from the DateAdded
                };

                _context.WaitingLists.Add(waitingEntry);
                _context.SaveChanges();

                TempData["Message"] = "You have been added to the waiting list.";
            }
            else
            {
                TempData["Error"] = "You are already on the waiting list for this book.";
            }

            return RedirectToAction("WaitingListView", new { bookId });
        }
        private async Task NotifyNextUsersInWaitingList(int bookId)
        {
            // Fetch the top 3 users from the waiting list
            var nextInLine = _context.WaitingLists
                .Where(w => w.BookId == bookId)
                .OrderBy(w => w.DateAdded)
                .Take(3)
                .ToList();

            foreach (var userEntry in nextInLine)
            {
                var user = _context.Users.SingleOrDefault(u => u.Id == userEntry.UserId);
                if (user != null)
                {
                    await SendEmailAsync(user.Email, "Book Available for Borrowing", $"The book you were waiting for is now available. Please borrow it within 5 days.");
                }

                // Remove the user from the waiting list after notification
                _context.WaitingLists.Remove(userEntry);
            }

            _context.SaveChanges();
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                using (var smtp = new SmtpClient("smtp.gmail.com", 587)) // Use port 587 for TLS
                {
                    // Set your Gmail credentials (use App Passwords if 2FA is enabled)
                    smtp.Credentials = new NetworkCredential("ranayoun11@gmail.com", "ubkg rapo lpjf pyjl");
                    smtp.EnableSsl = true; // Enable SSL/TLS

                    var mail = new MailMessage
                    {
                        From = new MailAddress("ranayoun11@gmail.com", "eBook Library"), // Add a display name
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true // Set to true if the email body contains HTML
                    };

                    mail.To.Add(toEmail);

                    // Debugging log
                    System.Diagnostics.Debug.WriteLine($"Sending email to {toEmail} with subject '{subject}'.");

                    // Send the email
                    await smtp.SendMailAsync(mail);

                    // Log successf
                    System.Diagnostics.Debug.WriteLine("Email sent successfully.");
                }
            }
            catch (SmtpException smtpEx)
            {
                // Log SMTP-specific errors
                System.Diagnostics.Debug.WriteLine($"SMTP Error: {smtpEx.Message}");
                TempData["Error"] = $"SMTP Error: {smtpEx.Message}";
            }
            catch (Exception ex)
            {
                // Log other errors
                System.Diagnostics.Debug.WriteLine($"Failed to send email: {ex.Message}");
                TempData["Error"] = $"Failed to send email: {ex.Message}";
            }
        }

        [HttpGet]
        public ActionResult DownloadBook(int bookId, string format)
        {
            // Validate the format
            var validFormats = new[] { "epub", "mobi", "pdf", "f2b" };
            if (string.IsNullOrEmpty(format) || !validFormats.Contains(format.ToLower()))
            {
                TempData["Error"] = "Invalid or unsupported format selected.";
                return RedirectToAction("Index");
            }

            // Fetch the book based on its ID
            var book = _context.Books.SingleOrDefault(b => b.Id == bookId);
            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("Index");
            }

            // Generate the file name and path
            string baseDirectory = System.Configuration.ConfigurationManager.AppSettings["BookFilesDirectory"];
            string fileName = $"{book.Title.Replace(" ", "_").ToLower()}.{format.ToLower()}";
            string filePath = Path.Combine(baseDirectory, fileName);

            // Debugging
            System.Diagnostics.Debug.WriteLine($"File Path: {filePath}");

            // Check if the file exists
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = $"The requested file '{fileName}' for the book '{book.Title}' in format '{format}' does not exist. Expected path: {filePath}";
                return RedirectToAction("Index");
            }

            // Determine MIME type using if-else
            string mimeType;
            if (format.ToLower() == "pdf")
            {
                mimeType = "application/pdf";
            }
            else if (format.ToLower() == "epub")
            {
                mimeType = "application/epub+zip";
            }
            else if (format.ToLower() == "mobi")
            {
                mimeType = "application/x-mobipocket-ebook";
            }
            else
            {
                mimeType = "application/octet-stream"; // Default MIME type for other formats
            }

            // Return the file for download
            return File(filePath, mimeType, fileName);
        }


        [HttpPost]
        public ActionResult ViewRating(int bookId)
        {
            // Fetch the logged-in user's ID
            int userId = GetCurrentUserId();
            if (userId == 0)
            {
                TempData["Error"] = "You must be logged in to view the rating.";
                return RedirectToAction("Index");
            }

            // Check if any ratings exist for this book
            bool anyRatingsExist = _context.Feedbacks.Any(f => f.BookId == bookId);
            if (!anyRatingsExist)
            {
                TempData["Error"] = "No ratings have been submitted for this book.";
                return RedirectToAction("Index");
            }

            // Check if the logged-in user has submitted feedback
            var userFeedback = _context.Feedbacks.SingleOrDefault(f => f.BookId == bookId && f.UserId == userId);

            // Calculate the average rating for the book
            var averageRating = _context.Feedbacks
                .Where(f => f.BookId == bookId)
                .Average(f => (double?)f.Rating) ?? 0;

            // Fetch the book details
            var book = _context.Books.SingleOrDefault(b => b.Id == bookId);
            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("Index");
            }

            // Handle the case where the logged-in user hasn't submitted feedback
            int? userRating = userFeedback != null ? (int?)userFeedback.Rating : null;

            // If no feedback exists for the logged-in user, display an informational message
            if (userFeedback == null)
            {
                TempData["Info"] = "You have not rated this book yet.";
            }

            // Set the ViewModel
            var model = new RatingViewModel
            {
                BookTitle = book.Title,
                UserRating = userRating.HasValue ? userRating.Value : 0, // Provide a default value if null
                AverageRating = averageRating,
                CoverImagePath = book.CoverImagePath // Pass the cover image path
            };

            // Pass the model to the view
            return View(model);
        }


        private int GetCurrentUserId()
        {
            if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int userId))
            {
                return userId;
            }
            return 0;
        }
    }
}
