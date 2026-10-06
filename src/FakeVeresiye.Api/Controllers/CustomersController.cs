using FakeVeresiye.Api.Dtos;
using FakeVeresiye.Api.Services.Customers;
using Microsoft.AspNetCore.Mvc;

namespace FakeVeresiye.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CustomerListItem>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null) =>
        Ok(await customers.List(page, pageSize, search));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDetailResponse>> Get(int id)
    {
        var response = await customers.Get(id);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerListItem>> Create(CreateCustomerRequest request)
    {
        var item = await customers.Create(request);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        await customers.Delete(id) ? NoContent() : NotFound();
}
