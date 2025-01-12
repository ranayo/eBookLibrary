using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using PayPal.Api;
using eBookLibrary.Models;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace eBookLibrary.Controllers
{
    public class PayPalController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PayPalController()
        {
            _context = new ApplicationDbContext();
        }

        // Action to handle the payment page
        [HttpPost]
        public ActionResult Payment(int? bookId = null)
        {
            if (bookId == null)
            {
                TempData["Error"] = "No book selected for payment.";
                return RedirectToAction("PersonalPage", "User");
            }

            var book = _context.Books.SingleOrDefault(b => b.Id == bookId.Value);
            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("PersonalPage", "User");
            }

            decimal priceToCharge = book.DiscountPrice ?? book.BuyPrice;
            ViewBag.PriceToCharge = priceToCharge;

            return View(book); // Pass book details to the view
        }

        [HttpPost]
        public ActionResult ProcessPayment(int bookId, decimal amount, bool? isBorrowed)
        {
            try
            {
                if (!isBorrowed.HasValue)
                {
                    TempData["Error"] = "Invalid request. Please specify if you want to borrow or buy.";
                    return RedirectToAction("PersonalPage", "User");
                }

                var book = _context.Books.SingleOrDefault(b => b.Id == bookId);
                if (book == null)
                {
                    TempData["Error"] = "Book not found.";
                    return RedirectToAction("PersonalPage", "User");
                }

                if (book.InStock <= 0)
                {
                    TempData["Error"] = "This book is out of stock.";
                    return RedirectToAction("PersonalPage", "User");
                }

                var config = GetPayPalConfig();
                var apiContext = new APIContext(new OAuthTokenCredential(config).GetAccessToken());

                // Set the description and price based on borrowing or buying
                string description = isBorrowed.Value ? $"{book.Title} (Borrow for 30 Days)" : $"{book.Title} (Purchase)";
                decimal finalAmount = isBorrowed.Value ? 2.00m : amount; // Borrow price is fixed at $2.00

                var payment = CreatePayment(apiContext,
                                            description,
                                            finalAmount,
                                            Url.Action("Return", "PayPal", new { bookId, isBorrowed }, Request.Url.Scheme),
                                            Url.Action("Cancel", "PayPal", null, Request.Url.Scheme),
                                            bookId);

                var approvalUrl = payment.links.FirstOrDefault(link => link.rel == "approval_url")?.href;
                if (!string.IsNullOrEmpty(approvalUrl))
                {
                    return Redirect(approvalUrl);
                }

                TempData["Error"] = "Unable to process payment. Please try again.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in ProcessPayment: {ex.Message}");
                TempData["Error"] = $"An error occurred: {ex.Message}";
            }

            return RedirectToAction("PersonalPage", "User");
        }


        public ActionResult Cancel()
        {
            TempData["Error"] = "Payment was cancelled.";
            return RedirectToAction("PersonalPage", "User");
        }

        [HttpPost]
        public ActionResult ProcessCartPayment()
        {
            var cart = Session["Cart"] as List<CartItem>;
            if (cart == null || !cart.Any())
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("PersonalPage", "User");
            }

            try
            {
                var config = GetPayPalConfig();
                var apiContext = new APIContext(new OAuthTokenCredential(config).GetAccessToken());
                var totalAmount = cart.Sum(item => item.FinalPrice * item.Quantity).ToString("F2");
                var cartId = Guid.NewGuid();
                Session["CartId"] = cartId;

                var payment = CreatePayment(apiContext,
                                            "Cart Purchase",
                                            decimal.Parse(totalAmount),
                                            Url.Action("Return", "PayPal", new { cartId }, Request.Url.Scheme),
                                            Url.Action("Cancel", "PayPal", null, Request.Url.Scheme),
                                            0);

                var approvalUrl = payment.links.FirstOrDefault(link => link.rel == "approval_url")?.href;
                if (!string.IsNullOrEmpty(approvalUrl))
                {
                    return Redirect(approvalUrl);
                }

                TempData["Error"] = "Unable to process payment. Please try again.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred: {ex.Message}";
            }

            return RedirectToAction("PersonalPage", "User");
        }

        private Dictionary<string, string> GetPayPalConfig()
        {
            return new Dictionary<string, string>
            {
                { "mode", "sandbox" },
                { "clientId", ConfigurationManager.AppSettings["PayPalClientId"] },
                { "clientSecret", ConfigurationManager.AppSettings["PayPalSecret"] }
            };
        }

        private Payment CreatePayment(APIContext apiContext, string description, decimal amount, string returnUrl, string cancelUrl, int bookId)
        {
            return new Payment
            {
                intent = "sale",
                payer = new Payer { payment_method = "paypal" },
                transactions = new List<Transaction>
                {
                    new Transaction
                    {
                        description = description,
                        amount = new Amount
                        {
                            currency = "USD",
                            total = amount.ToString("F2")
                        },
                        custom = bookId.ToString(),
                        item_list = new ItemList
                        {
                            items = new List<Item>
                            {
                                new Item
                                {
                                    name = description,
                                    currency = "USD",
                                    price = amount.ToString("F2"),
                                    quantity = "1"
                                }
                            }
                        }
                    }
                },
                redirect_urls = new RedirectUrls
                {
                    cancel_url = cancelUrl,
                    return_url = returnUrl
                }
            }.Create(apiContext);
        }
        private static async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                using (var smtp = new SmtpClient("smtp.gmail.com", 587))
                {
                    smtp.Credentials = new NetworkCredential("ranayoun11@gmail.com", "ubkg rapo lpjf pyjl");
                    smtp.EnableSsl = true;

                    var mail = new MailMessage
                    {
                        From = new MailAddress("ranayoun11@gmail.com", "eBook Library"),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    };

                    mail.To.Add(toEmail);

                    Console.WriteLine("Sending email...");
                    await smtp.SendMailAsync(mail);
                    Console.WriteLine("Email sent successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email: {ex.Message}");
            }
        }

        public async Task<ActionResult> Return(string paymentId, string PayerID, int? bookId, bool? isBorrowed)
        {
            try
            {
                if (!bookId.HasValue || !isBorrowed.HasValue)
                {
                    TempData["Error"] = "Invalid payment details.";
                    return RedirectToAction("PersonalPage", "User");
                }

                var userId = (int?)Session["UserId"];
                if (!userId.HasValue)
                {
                    TempData["Error"] = "You must be logged in to complete the payment.";
                    return RedirectToAction("Login", "User");
                }

                // Check if the user has already borrowed 3 books
                var borrowedBooksCount = _context.UserBooks
                    .Count(ub => ub.UserId == userId.Value && ub.IsBorrowed && ub.BorrowEndDate > DateTime.Now);

                if (isBorrowed.Value && borrowedBooksCount >= 3)
                {
                    TempData["Error"] = "You can only borrow up to 3 books at a time. Please return a book before borrowing a new one.";
                    return RedirectToAction("PersonalPage", "User");
                }

                // Proceed with executing the payment
                var config = GetPayPalConfig();
                var apiContext = new APIContext(new OAuthTokenCredential(config).GetAccessToken());
                var payment = new Payment { id = paymentId };
                var executedPayment = payment.Execute(apiContext, new PaymentExecution { payer_id = PayerID });

                if (executedPayment.state.ToLower() == "approved")
                {
                    // Add the book to the user's library
                    AddBookToUserLibrary(bookId.Value, isBorrowed.Value);

                    // Prepare email notification
                    var user = _context.Users.SingleOrDefault(u => u.Id == userId.Value);
                    var book = _context.Books.SingleOrDefault(b => b.Id == bookId.Value);

                    if (user != null && book != null)
                    {
                        var subject = isBorrowed.Value
                            ? "Book Borrow Confirmation"
                            : "Book Purchase Confirmation";

                        var body = isBorrowed.Value
                            ? $"You have successfully borrowed the book: {book.Title}. Please return it by {DateTime.Now.AddDays(30):yyyy-MM-dd}."
                            : $"You have successfully purchased the book: {book.Title}!";

                        // Send email notification
                        await SendEmailAsync(user.Email, subject, body);
                    }

                    // Set TempData message
                    TempData["Message"] = isBorrowed.Value
                        ? "You have successfully borrowed the book for 30 days!"
                        : "You have successfully purchased the book!";

                    return RedirectToAction("PersonalPage", "User");
                }
                else
                {
                    TempData["Error"] = "Payment failed.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Exception in Return: {ex.Message}");
            }

            return RedirectToAction("PersonalPage", "User");
        }


        private void AddBookToUserLibrary(int bookId, bool isBorrowed)
        {
            var userId = (int?)Session["UserId"];
            if (!userId.HasValue)
            {
                throw new UnauthorizedAccessException("User is not logged in.");
            }

            // Check if the user has already borrowed 3 books
            var borrowedBooksCount = _context.UserBooks
                .Count(ub => ub.UserId == userId.Value && ub.IsBorrowed && ub.BorrowEndDate > DateTime.Now);

            if (isBorrowed && borrowedBooksCount >= 3)
            {
                throw new InvalidOperationException("You can only borrow up to 3 books at a time. Please return a book before borrowing a new one.");
            }

            var book = _context.Books.SingleOrDefault(b => b.Id == bookId);
            if (book == null)
            {
                throw new InvalidOperationException("Book not found.");
            }

            if (isBorrowed)
            {
                // Check if there are available copies to borrow
                if (book.CopiesAvailable > 0)
                {
                    book.CopiesAvailable--; // Decrement available copies for borrowing
                }
                else
                {
                    throw new InvalidOperationException("No copies available to borrow.");
                }
            }
            else
            {
                // For buying, decrement InStock
                if (book.InStock > 0)
                {
                    book.InStock--; // Decrement the total stock for buying
                }
                else
                {
                    throw new InvalidOperationException("No stock available for this book.");
                }
            }

            // Add the book to the user's library
            var userBook = new UserBook
            {
                UserId = userId.Value,
                BookId = bookId,
                IsOwned = !isBorrowed, // If not borrowed, it is owned
                IsBorrowed = isBorrowed,
                BorrowEndDate = isBorrowed ? DateTime.Now.AddDays(30) : (DateTime?)null // Set borrow end date for borrowed books only
            };

            _context.UserBooks.Add(userBook);
            _context.SaveChanges();
        }


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
