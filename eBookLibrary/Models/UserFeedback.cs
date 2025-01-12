using System;
using System.ComponentModel.DataAnnotations;

namespace eBookLibrary.Models
{
    public class UserFeedback
    {
        [Key]
        public int Id { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [Required]
        [StringLength(500)]
        public string FeedbackContent { get; set; }

        public DateTime SubmittedAt { get; set; }
    }
}
