using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using VCA.API.Extensions;
using VCA.Domain.Entities;
using VCA.Domain.Enums;
using VCA.Domain.Interfaces;

namespace VCA.API.Controllers;

/// <summary>
/// Endpoints de doações — integração com Stripe (cartão) e Mercado Pago (Pix).
/// As sessões e QR codes são gerados via serviços externos na camada Infrastructure.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DonationsController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _configuration;

    public DonationsController(IUnitOfWork uow, IConfiguration configuration)
    {
        _uow = uow;
        _configuration = configuration;
    }

    /// <summary>
    /// Cria uma sessão de checkout Stripe para doação.
    /// Retorna a URL de redirecionamento para a página de pagamento do Stripe.
    /// </summary>
    [HttpPost("stripe/create-session")]
    [ProducesResponseType(typeof(StripeSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [SwaggerOperation(Summary = "Cria sessão de checkout Stripe para doação")]
    public async Task<ActionResult<StripeSessionResponse>> CreateStripeSession(
        [FromBody] CreateDonationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AmountBrl <= 0)
            return BadRequest(new ErrorResponse("O valor da doação deve ser positivo."));

        var userId = HttpContext.GetUserId();
        var user = await _uow.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return BadRequest(new ErrorResponse("Usuário não encontrado."));

        // Registra a intenção de doação com status pendente
        var donation = Donation.Create(userId, request.AmountBrl, DonationProvider.Stripe);
        await _uow.Donations.AddAsync(donation, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        // O StripeService (Infrastructure) deve ser injetado quando implementado.
        // Por ora retorna placeholder para que o frontend saiba que a doação foi registrada.
        var successUrl = _configuration["Stripe:SuccessUrl"] ?? "https://vintagecodeacademy.vercel.app/donate/success";
        var cancelUrl = _configuration["Stripe:CancelUrl"] ?? "https://vintagecodeacademy.vercel.app/donate/cancel";

        return Ok(new StripeSessionResponse(
            donation.Id,
            checkoutUrl: $"{successUrl}?donation={donation.Id}",
            "PENDING — integre StripeService na camada Infrastructure e substitua esta URL pela sessão real."));
    }

    /// <summary>
    /// Webhook do Stripe — processa eventos de pagamento confirmado.
    /// Não requer autenticação JWT; autenticidade verificada via Stripe-Signature header.
    /// </summary>
    [HttpPost("stripe/webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [SwaggerOperation(Summary = "Webhook Stripe para confirmação de pagamento")]
    public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        var webhookSecret = _configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
            return BadRequest(new ErrorResponse("Stripe webhook secret não configurado."));

        // Lê o corpo bruto para validação de assinatura
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        var stripeSignature = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(stripeSignature))
            return BadRequest(new ErrorResponse("Header Stripe-Signature ausente."));

        // TODO: Injetar StripeClient e validar evento com ConstructEvent quando StripeService for implementado.
        // Exemplo: var stripeEvent = EventUtility.ConstructEvent(payload, stripeSignature, webhookSecret);
        // Por ora retorna 200 para não bloquear o webhook da Stripe durante desenvolvimento.

        return Ok();
    }

    /// <summary>
    /// Gera um QR code Pix via Mercado Pago para doação.
    /// Retorna o código Pix copia-e-cola e a imagem base64 do QR code.
    /// </summary>
    [HttpPost("pix/create")]
    [ProducesResponseType(typeof(PixResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [SwaggerOperation(Summary = "Gera QR code Pix via Mercado Pago")]
    public async Task<ActionResult<PixResponse>> CreatePix(
        [FromBody] CreateDonationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AmountBrl <= 0)
            return BadRequest(new ErrorResponse("O valor da doação deve ser positivo."));

        var userId = HttpContext.GetUserId();
        var user = await _uow.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return BadRequest(new ErrorResponse("Usuário não encontrado."));

        var donation = Donation.Create(userId, request.AmountBrl, DonationProvider.MercadoPago);
        await _uow.Donations.AddAsync(donation, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        // TODO: Injetar IMercadoPagoService (Infrastructure) e gerar QR code real.
        // Por ora retorna placeholder para que o frontend possa renderizar o fluxo.

        return Ok(new PixResponse(
            donation.Id,
            PixCopyPaste: "00020126580014br.gov.bcb.pix0136PLACEHOLDER-DONATIONID-VCA",
            QrCodeBase64: "PLACEHOLDER",
            ExpiresAt: DateTime.UtcNow.AddMinutes(30)));
    }
}

public record CreateDonationRequest(decimal AmountBrl);
public record StripeSessionResponse(Guid DonationId, string CheckoutUrl, string Note);
public record PixResponse(Guid DonationId, string PixCopyPaste, string QrCodeBase64, DateTime ExpiresAt);
