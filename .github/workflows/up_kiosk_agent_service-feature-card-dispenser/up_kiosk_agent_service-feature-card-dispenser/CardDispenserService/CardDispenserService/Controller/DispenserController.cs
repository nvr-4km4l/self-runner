using CardDispenserAgent.Models;
using CardDispenserAgent.Services;
using Microsoft.AspNetCore.Mvc;

namespace CardDispenserAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DispenserController : ControllerBase
{
    private readonly CardDispenserService _dispenserService;

    public DispenserController(CardDispenserService dispenserService)
    {
        _dispenserService = dispenserService;
    }

    /// Returns the current hardware agent status.
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(_dispenserService.GetHealth());
    }

    /// Returns the current card dispenser status.
    [HttpGet("status")]
    [ProducesResponseType(typeof(DeviceStatus), StatusCodes.Status200OK)]
    public async Task<IActionResult> Status()
    {
        var result = await _dispenserService.GetStatusAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// Initializes the card dispenser.
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize()
    {
        var result = await _dispenserService.InitializeAsync();

        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }

    //C221 Push card
    [HttpPost("push")]
    public async Task<IActionResult> Push()
    {
        var result = await _dispenserService.PushCardAsync();
        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }

    /// Dispenses one card.
    [HttpPost("dispense")]
    public async Task<IActionResult> Dispense()
    {
        var result = await _dispenserService.DispenseCardAsync();

        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }

    /// Captures the card into the reject bin.
    [HttpPost("eject")]
    public async Task<IActionResult> Capture()
    {
        var result = await _dispenserService.EjectCardAsync();

        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }

    /// Ejects the card to the front slot.
    //[HttpPost("eject")]
    //public async Task<IActionResult> Eject()
    //{
    //    var result = await _dispenserService.EjectCardAsync();

    //    if (!result.IsSuccess)
    //        return BadRequest(result);

    //    return Ok(result);
    //}

    [HttpGet("hopper")]
    public async Task<IActionResult> HopperStatus()
    {
        var result = await _dispenserService.GetHopperStatusAsync();

        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }
}