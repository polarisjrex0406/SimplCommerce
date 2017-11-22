﻿using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Core.Models;
using SimplCommerce.Module.Orders.Services;
using SimplCommerce.Module.Orders.ViewModels;
using SimplCommerce.Module.ShippingPrices.Services;

namespace SimplCommerce.Module.Orders.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IRepository<Country> _countryRepository;
        private readonly IRepository<StateOrProvince> _stateOrProvinceRepository;
        private readonly IRepository<UserAddress> _userAddressRepository;
        private readonly IShippingPriceService _shippingPriceService;
        private readonly IWorkContext _workContext;

        public CheckoutController(
            IRepository<StateOrProvince> stateOrProvinceRepository,
            IRepository<Country> countryRepository,
            IRepository<UserAddress> userAddressRepository,
            IShippingPriceService shippingPriceService,
            IOrderService orderService,
            IWorkContext workContext)
        {
            _stateOrProvinceRepository = stateOrProvinceRepository;
            _countryRepository = countryRepository;
            _userAddressRepository = userAddressRepository;
            _shippingPriceService = shippingPriceService;
            _orderService = orderService;
            _workContext = workContext;
        }

        public IActionResult Index()
        {
            return RedirectToAction("DeliveryInformation");
        }

        [HttpGet]
        public async Task<IActionResult> DeliveryInformation()
        {
            var model = new DeliveryInformationVm();

            var currentUser = await _workContext.GetCurrentUser();
            model.ExistingShippingAddresses = _userAddressRepository
                .Query()
                .Where(x => (x.AddressType == AddressType.Shipping) && (x.UserId == currentUser.Id))
                .Select(x => new ShippingAddressVm
                {
                    UserAddressId = x.Id,
                    ContactName = x.Address.ContactName,
                    Phone = x.Address.Phone,
                    AddressLine1 = x.Address.AddressLine1,
                    AddressLine2 = x.Address.AddressLine1,
                    DistrictName = x.Address.District.Name,
                    StateOrProvinceName = x.Address.StateOrProvince.Name,
                    CountryName = x.Address.Country.Name
                }).ToList();

            model.ShippingAddressId = currentUser.DefaultShippingAddressId ?? 0;

            model.NewAddressForm.ShipableContries = _countryRepository.Query()
                .Where(x => x.IsShippingEnabled)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Text = x.Name,
                    Value = x.Id.ToString()
                }).ToList();

            if(model.NewAddressForm.ShipableContries.Count == 1)
            {
                var onlyShipableCountryId = long.Parse(model.NewAddressForm.ShipableContries.First().Value);
                model.NewAddressForm.StateOrProvinces = _stateOrProvinceRepository
                .Query()
                .Where(x => x.CountryId == onlyShipableCountryId)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Text = x.Name,
                    Value = x.Id.ToString()
                }).ToList();
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> DeliveryInformation(DeliveryInformationVm model)
        {
            if (!ModelState.IsValid && (model.ShippingAddressId == 0))
            {
                return View(model);
            }

            var currentUser = await _workContext.GetCurrentUser();
            Address billingAddress;
            Address shippingAddress;

            if (model.ShippingAddressId == 0)
            {
                var address = new Address
                {
                    AddressLine1 = model.NewAddressForm.AddressLine1,
                    AddressLine2 = model.NewAddressForm.AddressLine2,
                    ContactName = model.NewAddressForm.ContactName,
                    CountryId = model.NewAddressForm.CountryId,
                    StateOrProvinceId = model.NewAddressForm.StateOrProvinceId,
                    DistrictId = model.NewAddressForm.DistrictId,
                    City = model.NewAddressForm.City,
                    PostalCode = model.NewAddressForm.PostalCode,
                    Phone = model.NewAddressForm.Phone
                };

                var userAddress = new UserAddress
                {
                    Address = address,
                    AddressType = AddressType.Shipping,
                    UserId = currentUser.Id
                };

                _userAddressRepository.Add(userAddress);

                billingAddress = shippingAddress = address;
            }
            else
            {
                billingAddress = shippingAddress = _userAddressRepository.Query().Where(x => x.Id == model.ShippingAddressId).Select(x => x.Address).First();
            }

            await _orderService.CreateOrder(currentUser, billingAddress, shippingAddress);

            return RedirectToAction("OrderConfirmation");
        }

        [HttpPost]
        public async Task<IActionResult> GetTaxAndShippingPrice([FromBody] TaxAndShippingPriceRequestVm model)
        {
            var currentUser = await _workContext.GetCurrentUser();
            Address address;
            if (model.ExistingShippingAddressId != 0)
            {
                address = await _userAddressRepository.Query().Where(x => x.Id == model.ExistingShippingAddressId).Select(x => x.Address).FirstOrDefaultAsync();
                if (address == null)
                {
                    return NotFound();
                }
            }
            else
            {
                address = new Address
                {
                    CountryId = model.NewShippingAddress.CountryId,
                    StateOrProvinceId = model.NewShippingAddress.StateOrProvinceId,
                    AddressLine1 = model.NewShippingAddress.AddressLine1
                };
            }

            var request = new GetShippingPriceRequest
            {
                OrderAmount = model.OrderAmount,
                ShippingAddress = address
            };

            var orderTaxAndShippingPrice = new OrderTaxAndShippingPriceVm();
            orderTaxAndShippingPrice.ShippingPrices = await _shippingPriceService.GetApplicableShippingPrices(request);
            orderTaxAndShippingPrice.TaxAmount = await _orderService.GetTax(currentUser.Id, address.CountryId, address.StateOrProvinceId);
            return Ok(orderTaxAndShippingPrice);
        }

        [HttpGet]
        public IActionResult OrderConfirmation()
        {
            return View();
        }
    }
}