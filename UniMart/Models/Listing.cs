using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Plugin.CloudFirestore.Attributes;

namespace UniMart.Models
{
    public class Listing
    {
        [Id]
        public string Id { get; set; }

        [MapTo("sellerId")]
        public string SellerId { get; set; }

        [MapTo("sellerName")]
        public string SellerName { get; set; }

        [MapTo("title")]
        public string Title { get; set; }

        [MapTo("description")]
        public string Description { get; set; }

        [MapTo("price")]
        public double Price { get; set; }

        [MapTo("status")] // Available, Reserved, Sold, Disputed
        public string Status { get; set; }

        [MapTo("category")]
        public string Category { get; set; }

        [MapTo("condition")]
        public string Condition { get; set; }

        [MapTo("createdAt")]
        public DateTime CreatedAt { get; set; }

        [MapTo("soldToBuyerId")]
        public string SoldToBuyerId { get; set; }

        [MapTo("imageUrls")]
        public List<string> ImageUrls { get; set; } = new List<string>();

        [Plugin.CloudFirestore.Attributes.Ignored]
        public string CoverImage => (ImageUrls != null && ImageUrls.Count > 0) ? ImageUrls[0] : "placeholder_image.png";

        // 🔥 NEW: The TimeAgo Engine
        [Plugin.CloudFirestore.Attributes.Ignored]
        public string TimeAgo
        {
            get
            {
                if (CreatedAt == default) return "Just now";

                var timeSpan = DateTime.Now - CreatedAt.ToLocalTime();

                if (timeSpan <= TimeSpan.FromSeconds(60)) return "Just now";
                if (timeSpan <= TimeSpan.FromMinutes(60)) return timeSpan.Minutes > 1 ? $"{timeSpan.Minutes}m ago" : "1m ago";
                if (timeSpan <= TimeSpan.FromHours(24)) return timeSpan.Hours > 1 ? $"{timeSpan.Hours}h ago" : "1h ago";
                if (timeSpan <= TimeSpan.FromDays(30)) return timeSpan.Days > 1 ? $"{timeSpan.Days}d ago" : "1d ago";
                if (timeSpan <= TimeSpan.FromDays(365)) return $"{timeSpan.Days / 30}mo ago";
                return $"{timeSpan.Days / 365}y ago";
            }
        }

        // 🔥 NEW: Linker Shield to prevent Release Mode crashes
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Listing))]
        public Listing() { }

        public void Preserve() { }
    }
}