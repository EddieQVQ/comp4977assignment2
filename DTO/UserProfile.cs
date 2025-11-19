using System;

namespace assignment2.DTO
{
    public class UserProfileResponse
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }

        public string FullName => $"{FirstName} {LastName}";
    }
}