using Plugin.CloudFirestore.Attributes;
using System;

namespace UniMart.Models
{
    public class User
    {
        [Id]
        public string Id { get; set; }

        [MapTo("email")]
        public string Email { get; set; }

        [MapTo("tenantId")]
        public string TenantId { get; set; }

        [MapTo("name")]
        public string Name { get; set; }

        [MapTo("major")]
        public string Major { get; set; }

        [MapTo("studentId")]
        public string StudentId { get; set; }

        [MapTo("accountStatus")]
        public string AccountStatus { get; set; }

        [MapTo("wallet")]
        public Wallet Wallet { get; set; }

        public User()
        {
            Wallet = new Wallet();
        }

        // 🔥 THE LINKER SHIELD: Forces the compiler to keep this class alive
        [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof(User))]
        public static void Preserve() { new User(); }
    }

    public class Wallet
    {
        [MapTo("availableBalance")]
        public double AvailableBalance { get; set; } = 0.0;

        [MapTo("pendingEscrow")]
        public double PendingEscrow { get; set; } = 0.0;

        [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof(Wallet))]
        public static void Preserve() { new Wallet(); }
    }
}