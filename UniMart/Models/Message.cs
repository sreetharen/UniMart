using Plugin.CloudFirestore.Attributes;
using System;

namespace UniMart.Models
{
    public class Message
    {
        [Id]
        public string Id { get; set; }

        [MapTo("offerId")]
        public string OfferId { get; set; }

        [MapTo("senderId")]
        public string SenderId { get; set; }

        [MapTo("text")]
        public string Text { get; set; }

        [MapTo("timestamp")]
        public DateTime Timestamp { get; set; }

        [MapTo("messageType")]
        public string MessageType { get; set; } = "text";

        [MapTo("mediaUrl")]
        public string MediaUrl { get; set; }

        // 🔥 UI HELPERS (Ignored by Firebase Database)
        [Ignored]
        public bool IsMine { get; set; }

        [Ignored]
        public bool HasMedia => !string.IsNullOrEmpty(MediaUrl);

        [Ignored]
        public bool HasText => !string.IsNullOrWhiteSpace(Text);

        [Ignored]
        public bool ShowDateHeader { get; set; }

        [Ignored]
        public string DateHeaderText { get; set; }

        [Ignored]
        public string FormattedMytTime { get; set; }
    }
}