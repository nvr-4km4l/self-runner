using CardDispenserAgent.Dto.Request;
using CardDispenserAgent.Dto.Response;
using CardDispenserAgent.Services;
using Microsoft.AspNetCore.Mvc;

namespace CardDispenserAgent.Controllers
{
    [ApiController]
    [Route("api/device")]
    public class DeviceController : ControllerBase
    {
        private readonly DeviceIdentityService _deviceIdentityService;

        public DeviceController(
            DeviceIdentityService deviceIdentityService)
        {
            _deviceIdentityService = deviceIdentityService;
        }

        [HttpGet("device-details")]
        public IActionResult GetIdentity()
        {
            var identity = _deviceIdentityService.GetIdentity();

            var response = new CommissionResponseDto
            {
                DeviceId = identity.DeviceId,
                DeviceName = identity.DeviceName,
                MacAddress = identity.MacAddress,
                IpAddress = identity.IpAddress
            };

            return Ok(response);
        }

        [HttpPost("identity")]
        public IActionResult GetIdentity([FromBody] CommissionRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.IpAddress))
            {
                return BadRequest(new
                {
                    message = "IP address is required."
                });
            }

            var isMatch = _deviceIdentityService
                .IsIpAddressMatch(request.IpAddress);

            if (!isMatch)
            {
                return BadRequest(new
                {
                    message = "The IP address does not match this device."
                });
            }

            var identity = _deviceIdentityService.GetIdentity();

            var response = new CommissionResponseDto
            {
                DeviceId = identity.DeviceId,
                DeviceName = identity.DeviceName,
                MacAddress = identity.MacAddress,
                IpAddress = identity.IpAddress
            };

            return Ok(response);
        }
    }
}