using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace eBookLibrary.Models
{
    public class WaitingList
    {
        public int Id { get; set; }
        public int BookId { get; set; } // Foreign key to the Book
        public int UserId { get; set; } // User waiting for the book
        public DateTime DateAdded { get; set; }
        public DateTime? ExpectedAvailabilityDate { get; set; } // Nullable to handle cases where it's not set


    }

}