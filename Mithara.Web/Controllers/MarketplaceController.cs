using Microsoft.AspNetCore.Mvc;
using Mithara.Web.Models.ViewModels;
using Mithara.Web.Services;

namespace Mithara.Web.Controllers;

public sealed class MarketplaceController : Controller
{
    private readonly MarketplaceService _marketplace;

    public MarketplaceController(MarketplaceService marketplace)
    {
        _marketplace = marketplace;
    }

    public async Task<IActionResult> Index(string search = "", int type = -1)
    {
        var listings = await _marketplace.GetListingsAsync(search, type);
        return View(new MarketplaceIndexViewModel
        {
            Search = search,
            ItemType = type,
            Listings = listings,
        });
    }
}
