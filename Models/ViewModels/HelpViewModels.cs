using FYP.Models.Entities;
using System.Collections.Generic;

namespace FYP.Models.ViewModels
{
    public class HelpCenterIndexViewModel
    {
        public List<HelpCategory> Categories { get; set; } = new List<HelpCategory>();
        public List<HelpArticle> PopularArticles { get; set; } = new List<HelpArticle>();
    }

    public class HelpCategoryViewModel
    {
        public HelpCategory Category { get; set; }
        public List<HelpArticle> Articles { get; set; } = new List<HelpArticle>();
    }

    public class HelpSearchResultViewModel
    {
        public string Query { get; set; } = string.Empty;
        public List<HelpArticle> Results { get; set; } = new List<HelpArticle>();
    }
}
