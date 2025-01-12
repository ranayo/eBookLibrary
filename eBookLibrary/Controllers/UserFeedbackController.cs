using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using eBookLibrary.Models;

namespace eBookLibrary.Controllers
{
    public class UserFeedbackController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserFeedbackController()
        {
            _context = new ApplicationDbContext();
        }

        // GET: UserFeedback
        public ActionResult Index()
        {
            var feedbacks = _context.UserFeedbacks
                .OrderByDescending(f => f.SubmittedAt)
                .ToList();

            return View(feedbacks);
        }

        // POST: UserFeedback/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(int rating, string feedbackContent)
        {
            if (string.IsNullOrWhiteSpace(feedbackContent))
            {
                TempData["Error"] = "Feedback cannot be empty.";
                return RedirectToAction("Index");
            }

            var feedback = new UserFeedback
            {
                Rating = rating,
                FeedbackContent = feedbackContent,
                SubmittedAt = DateTime.Now
            };

            _context.UserFeedbacks.Add(feedback);
            _context.SaveChanges();

            TempData["Message"] = "Thank you for your feedback!";
            return RedirectToAction("Index");
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

