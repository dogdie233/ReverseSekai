using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using MessagePack;
using SelfHostSekai.Cryptography;

namespace SelfHostSekai.Formatters;

public class EncryptedMessagePackInputFormatter : InputFormatter
{
    private const string ContentType = "application/octet-stream";
    private readonly AesCryptoHelper _cryptoHelper;

    public EncryptedMessagePackInputFormatter(AesCryptoHelper cryptoHelper)
    {
        _cryptoHelper = cryptoHelper;
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(ContentType));
    }

    protected override bool CanReadType(Type type) => true;

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        var request = context.HttpContext.Request;

        using var memoryStream = new MemoryStream();
        await request.Body.CopyToAsync(memoryStream);
        var encryptedBytes = memoryStream.ToArray();

        if (encryptedBytes.Length == 0)
        {
            return await InputFormatterResult.SuccessAsync(null);
        }

        try
        {
            var decryptedBytes = _cryptoHelper.Decrypt(encryptedBytes);
            var result = MessagePackSerializer.Deserialize(context.ModelType, decryptedBytes);
            return await InputFormatterResult.SuccessAsync(result);
        }
        catch (Exception ex)
        {
            context.ModelState.TryAddModelError(context.ModelName, ex.Message);
            return await InputFormatterResult.FailureAsync();
        }
    }
}