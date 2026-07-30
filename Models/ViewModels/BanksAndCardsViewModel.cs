using FYP.Models.Entities;
using System.Collections.Generic;

namespace FYP.Models.ViewModels
{
    public class BanksAndCardsViewModel
    {
        public List<SavedBankCard> SavedCards { get; set; } = new List<SavedBankCard>();
        public string StripePublishableKey { get; set; } = string.Empty;
    }
}
