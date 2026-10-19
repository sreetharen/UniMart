<div align="center">

# 🛒 UniMart

### A secure campus marketplace for UNITEN students, with escrow payments and QR-verified handovers

*Final Year Project, Bachelor of Information Technology (Information Systems) (Hons), Universiti Tenaga Nasional*

<img src="https://github.com/user-attachments/assets/19cf3872-aeda-486b-9448-ee137edcfee0" width="250">

</div>

---

## 📌 Overview

UniMart is a mobile marketplace where UNITEN students can buy and sell second-hand items on campus. This repository contains the **student mobile app**. Moderators use a separate web console, [UniMart.Admin](https://github.com/sreetharen/UniMart.Admin).

Today, students trade through hostel-based Telegram groups. Listings get buried under announcements and chatter, students outside a particular hostel group never see them, and there is no search, no categories, and no protection against no-show buyers or fraud. UniMart replaces that with one organised, trusted platform.

**The core idea:** when a buyer reserves an item, the money is locked in escrow inside the app. It is only released to the seller after the seller scans the buyer's QR code at the physical handover. Nobody has to trust a stranger up front.

## 🔍 The Problem (from my survey)

I surveyed **50 students** before designing the system:

| Finding | Result |
|---|---|
| Used Telegram groups for item exchanges | ~90% |
| Found it hard to locate specific items | 86% |
| Wanted search or filtering options that don't exist | 81% |

## ✨ Features

- **Verified-only access:** login is restricted to `@student.uniten.edu.my` accounts, with no public sign-up
- **Categorised listings and real-time search** so items are easy to find
- **Create and manage listings** with photo upload and a 500-character description limit
- **In-app real-time chat** tied to each listing, with status badges (Accepted, Cancelled, Completed), unread counters, and search
- **Digital wallet** showing *Available Balance* and *Locked in Escrow* separately, with a Stripe top-up integration point
- **Escrow transactions:** reserving an item locks the buyer's funds and hides the listing from the feed
- **QR-code handover:** the buyer shows a unique QR code and the seller scans it with the phone camera, which releases the funds
- **Report system** to flag suspicious listings or users, which moderators review in the admin console

## 🔄 How the Escrow Flow Works

```
 Buyer                         UniMart                        Seller
   │                              │                              │
   │── Reserve & Lock ──────────▶ │                              │
   │                              │  Funds move: Available →     │
   │                              │  Escrow. Listing hidden.     │
   │◀── Unique handover QR ────── │                              │
   │                              │                              │
   │        ( meet on campus, buyer inspects the item )          │
   │                              │                              │
   │── shows QR ─────────────────────────────────────────────── ▶│
   │                              │◀──── Seller scans QR ────────│
   │                              │  Escrow cleared → seller's   │
   │                              │  available balance           │
```

## 🛠️ Tech Stack

| Layer | Technology |
|---|---|
| Mobile app | **.NET MAUI** (C#, XAML), built for Android |
| Database | **Cloud Firestore** (NoSQL, real-time listeners) |
| Authentication | **Firebase Authentication** |
| Image storage | **Cloudinary** (.NET SDK, CDN delivery) |
| QR scanning | **ZXing.Net.MAUI** (camera integration) |
| Payments (wallet top-up) | **Stripe** |
| Tooling | Visual Studio 2022, Git, Figma |

**Design notes**
- Dependency injection keeps services (cloud connectors, escrow engine, QR generation) separate from the UI.
- Firestore's live listeners keep the marketplace feed and wallet/escrow balances in sync without refreshing.

**Related repository:** the moderator web console (Blazor) lives in [UniMart.Admin](https://github.com/sreetharen/UniMart.Admin). It shares the same Firebase backend.

## 🗂️ Data Model

Firestore collections: **Users**, **Listings**, **Offers** (reservations), and **Messages**.

## 📱 Screenshots

| Login | Marketplace | Item details |
|:---:|:---:|:---:|
| <img src="https://github.com/user-attachments/assets/da81ad0f-17f5-4088-b857-d5401a7e24c7" width="220"> | <img src="https://github.com/user-attachments/assets/a4f58c4c-439e-4f6c-b563-3aeb5c52635e" width="220"> | <img src="https://github.com/user-attachments/assets/04cc8785-6d0a-4a19-8ae6-70a99af3e2ed" width="220"> |

| Wallet & escrow | Handover QR |
|:---:|:---:|
| <img src="https://github.com/user-attachments/assets/32a4d7f9-313f-4ed7-a0a8-8212ab2509d6" width="220"> | <img src="https://github.com/user-attachments/assets/a3b8623c-ccac-4da7-a4f4-1f5fd07b46df" width="220"> |

## ✅ Testing

| Type | What was tested | Result |
|---|---|---|
| Unit | Student-email validation, wallet/escrow calculation (RM 70 balance, RM 50 item → RM 20 available, RM 50 in escrow), 500-character limit | 3 / 3 passed |
| Integration | App ↔ Firebase connection and real-time updates | Passed |
| System | Reserve & lock flow, QR scanner hardware, automatic fund release | 3 / 3 passed |
| User acceptance | Buyer and seller workflows on a physical Android device with 2 testers | Passed |

**What testing taught me:** testers found scanning difficult in low light, and the *Available* vs *Locked in Escrow* amounts needed clearer visual separation. Both are on the roadmap below.

## ⚠️ Known Limitations

- Handover QR generation and scanning need an internet connection
- Scanning depends on camera quality and lighting
- Wallet top-ups only go through Stripe, with no FPX support
- Built and tested for Android only

## 🚀 Roadmap

- [ ] Flashlight toggle on the QR scanner for evening handovers
- [ ] Offline QR verification (Bluetooth or NFC time-limited tokens)
- [ ] FPX and Touch 'n Go eWallet top-ups
- [ ] Colour-coded wallet balances

## ⚙️ Getting Started

**Prerequisites:** Visual Studio 2022 with the *.NET Multi-platform App UI* workload, an Android emulator or device, and your own Firebase, Cloudinary, and Stripe accounts.

```bash
git clone https://github.com/sreetharen/UniMart.git
```

Open the solution in Visual Studio 2022, add your own API keys, and run on an Android device or emulator. API keys are not included in this repository. You will need:

- A Firebase project (Authentication and Firestore)
- Cloudinary cloud name and API credentials
- Stripe publishable key

## 👤 Author

**Sreetharen Mohanaveloo**
Information Systems graduate, UNITEN
[LinkedIn](http://www.linkedin.com/in/sreetharen-mohanaveloo-58bbb33b1) · [GitHub](https://github.com/sreetharen) · sreetharenmohanaveloo04@gmail.com

*Supervisor: Mr. Lim Fung Chen*
