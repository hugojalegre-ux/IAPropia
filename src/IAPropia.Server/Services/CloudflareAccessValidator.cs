using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using IAPropia.Models;
using Microsoft.IdentityModel.Tokens;

namespace IAPropia.Services;

/// <summary>Valida Cf-Access-Jwt-Assertion contra certificados de Cloudflare Access.</summary>
public sealed class CloudflareAccessValidator
{
    private const string JwtHeaderName = "Cf-Access-Jwt-Assertion";

    private readonly SeguridadOptions _seg;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IaFileLog _log;
    private readonly object _certLock = new();
    private ICollection<SecurityKey>? _cachedKeys;
    private DateTime _certFetchedUtc = DateTime.MinValue;

    public CloudflareAccessValidator(IaPropiaOptions opts, IHttpClientFactory httpFactory, IaFileLog log)
    {
        _seg = opts.Seguridad;
        _httpFactory = httpFactory;
        _log = log;
    }

    public async Task<(bool Ok, string? Error)> ValidarAsync(HttpContext context, CancellationToken ct = default)
    {
        if (!_seg.ValidarCloudflareAccess)
            return (true, null);

        var team = _seg.CloudflareTeamDomain?.Trim().TrimEnd('/');
        var aud = _seg.AudienciaAccess?.Trim();
        if (string.IsNullOrEmpty(team) || string.IsNullOrEmpty(aud))
            return (false, "ValidarCloudflareAccess activo: completá CloudflareTeamDomain y AudienciaAccess en secrets.");

        if (!context.Request.Headers.TryGetValue(JwtHeaderName, out var token) || string.IsNullOrWhiteSpace(token))
            return (false, "Falta JWT de Cloudflare Access (Cf-Access-Jwt-Assertion). ¿Iniciaste sesión en Access?");

        try
        {
            var keys = await ObtenerClavesAsync(team, ct);
            var handler = new JwtSecurityTokenHandler();
            var iss = $"https://{team}";
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = iss,
                ValidateAudience = true,
                ValidAudience = aud,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keys,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            handler.ValidateToken(token!, parameters, out _);
            return (true, null);
        }
        catch (Exception ex)
        {
            _log.LogError($"cloudflare access jwt: {ex.Message}");
            return (false, "JWT de Cloudflare Access inválido o vencido.");
        }
    }

    private async Task<ICollection<SecurityKey>> ObtenerClavesAsync(string teamDomain, CancellationToken ct)
    {
        lock (_certLock)
        {
            if (_cachedKeys != null && DateTime.UtcNow - _certFetchedUtc < TimeSpan.FromHours(1))
                return _cachedKeys;
        }

        var url = $"https://{teamDomain}/cdn-cgi/access/certs";
        var http = _httpFactory.CreateClient("llm-web");
        var pem = await http.GetStringAsync(url, ct);
        var keys = new List<SecurityKey>();

        foreach (Match m in Regex.Matches(pem, "-----BEGIN CERTIFICATE-----[\\s\\S]*?-----END CERTIFICATE-----"))
        {
            try
            {
                var cert = X509Certificate2.CreateFromPem(m.Value);
                keys.Add(new X509SecurityKey(cert));
            }
            catch
            {
                // siguiente cert
            }
        }

        if (keys.Count == 0)
            throw new InvalidOperationException("No se pudieron cargar certificados Cloudflare Access.");

        lock (_certLock)
        {
            _cachedKeys = keys;
            _certFetchedUtc = DateTime.UtcNow;
        }

        return keys;
    }
}
