using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace TraceCore.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("default")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ISender Sender { get; }

    protected ApiControllerBase(ISender sender)
    {
        Sender = sender;
    }
}
