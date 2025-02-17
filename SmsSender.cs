using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Exceptions;

public class SmsSender
{
    private readonly ILogger<Worker> _logger;
    private readonly string _twilioPhoneNumber;
    private readonly string _accountSid;
    private readonly string _authToken;

    public SmsSender(ILogger<Worker> logger)
    {
        _logger = logger;

        // Provide fallback values or throw exceptions
        _accountSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID") 
                    ?? throw new InvalidOperationException("TWILIO_ACCOUNT_SID is not set.");
        _authToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN") 
                    ?? throw new InvalidOperationException("TWILIO_AUTH_TOKEN is not set.");
        _twilioPhoneNumber = Environment.GetEnvironmentVariable("TWILIO_PHONE_NUMBER") 
                    ?? throw new InvalidOperationException("TWILIO_PHONE_NUMBER is not set.");

    }

    public async Task SendSmsAsync(string phoneNumber, string messageBody)
    {
        TwilioClient.Init(_accountSid, _authToken);

        try
        {
            var message = await MessageResource.CreateAsync(
                to: new Twilio.Types.PhoneNumber(phoneNumber),
                from: new Twilio.Types.PhoneNumber(_twilioPhoneNumber),
                body: messageBody
            );

            _logger.LogInformation($"[x] SMS enviado a {phoneNumber}: {message.Sid}");
        }
        catch (ApiException ex)
        {
            _logger.LogError($"[!] Error al enviar SMS a {phoneNumber}: {ex.Message}");
        }
    }
}