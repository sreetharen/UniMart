using Plugin.CloudFirestore.Attributes;
using System;

namespace UniMart.Models
{
    public class TransactionRecord
    {
        [Id]
        public string Id { get; set; }

        [MapTo("userId")]
        public string UserId { get; set; }

        [MapTo("amount")]
        public double Amount { get; set; }

        [MapTo("type")] // TopUp, Purchase, Sale, Refund, Withdrawal
        public string Type { get; set; }

        [MapTo("description")]
        public string Description { get; set; }

        [MapTo("date")]
        public DateTime Date { get; set; }

        // --- UI HELPERS ---

        [Ignored] // Converts UTC to Malaysia Time (UTC+8)
        public string LocalDisplayDate => Date.ToLocalTime().ToString("MMM dd, yyyy • hh:mm tt");

        [Ignored]
        public string DisplayAmount => (Type == "TopUp" || Type == "Refund" || Type == "Sale") 
            ? $"+ RM {Amount:F2}" 
            : $"- RM {Amount:F2}";

        [Ignored]
        public string AmountColor => (Type == "TopUp" || Type == "Refund" || Type == "Sale") 
            ? "#28A745" // Success Green
            : "#1A1A1A"; // Premium Dark Neutral

        [Ignored]
        public string IconText => Type switch
        {
            "TopUp" => "TU",
            "Purchase" => "PR",
            "Sale" => "SL",
            "Refund" => "RF",
            "Withdrawal" => "WD",
            _ => "TX"
        };
        // 🔥 NEW: Linker Shield to prevent Release Mode crashes
        [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof(TransactionRecord))]
        public TransactionRecord() { }

        public void Preserve() { }
    }
}