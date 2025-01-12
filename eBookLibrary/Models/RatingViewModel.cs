using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace eBookLibrary.Models
{
    public class RatingViewModel
    {
        public string BookTitle { get; set; }
        public int UserRating { get; set; }
        public double AverageRating { get; set; }
        public string CoverImagePath { get; set; } // Path to the book's image

    }
}