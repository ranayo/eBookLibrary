﻿using System;
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
            try
            {
                // Fetch feedback from the database, order by most recent
                var feedbacks = _context.UserFeedbacks
                    .OrderByDescending(f => f.SubmittedAt)
                    .ToList();

                // If no feedbacks exist, create an empty list
                if (feedbacks == null || !feedbacks.Any())
                {
                    feedbacks = new List<UserFeedback>();
                }

                // Fetch the total number of books
                var totalBooks = _context.Books.Count();

                // Pass feedbacks and totalBooks to the view
                ViewBag.TotalBooks = totalBooks;

                return View(feedbacks);
            }
            catch (Exception ex)
            {
                // Handle exceptions and log them (optional)
                TempData["Error"] = "An error occurred while fetching feedback. Please try again.";
                return View(new List<UserFeedback>()); // Return an empty list to avoid crashes
            }
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

            try
            {
                // Create a new feedback record
                var feedback = new UserFeedback
                {
                    Rating = rating,
                    FeedbackContent = feedbackContent,
                    SubmittedAt = DateTime.Now
                };

                // Save the feedback to the database
                _context.UserFeedbacks.Add(feedback);
                _context.SaveChanges();

                TempData["Message"] = "Thank you for your feedback!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while saving your feedback. Please try again.";
                // Optionally log the exception (not shown here)
            }

            // Redirect to Index to refresh the feedback list
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
