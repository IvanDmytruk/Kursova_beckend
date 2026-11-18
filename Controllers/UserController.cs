using Beckend.DTOs;
using Beckend.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beckend.Controllers
{
    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        private static object PublicProfile(UserDto user)
        {
            return new
            {
                user.Id,
                user.Name,
                user.Surname,
                user.Age,
                user.Role
            };
        }

        [AllowAnonymous]
        [HttpGet("search")]
        public async Task<IActionResult> SearchPlCo(
            [FromQuery] string term)
        {
            try
            {
                var users = await _userService
                    .SearchPlayersAndCoachesAsync(term);

                return Ok(users.Select(PublicProfile).ToList());
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("search/{id}")]
        public async Task<IActionResult> SearchPlCoByID(string id)
        {
            try
            {
                var user = await _userService
                    .GetPlayerOrCoachByIdAsync(id);

                return Ok(PublicProfile(user));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return NotFound();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [AllowAnonymous]
        [HttpGet("age-range")]
        public async Task<IActionResult> GetByAgeRange(
            int minAge,
            int maxAge)
        {
            try
            {
                var users = await _userService
                    .GetPlayersAndCoachesByAgeRangeAsync(
                        minAge, maxAge);

                return Ok(users.Select(PublicProfile).ToList());
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllAsync();
            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _userService.GetByIdAsync(id);

            if (user == null)
                return NotFound();

            return Ok(user);
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateUserDto createDto)
        {
            try
            {
                var created = await _userService
                    .CreateAsync(createDto);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = created.Id },
                    created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
            string id,
            [FromBody] UpdateUserDto updateDto)
        {
            try
            {
                var updated = await _userService
                    .UpdateAsync(id, updateDto);

                return Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var deleted = await _userService.DeleteAsync(id);

            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}
