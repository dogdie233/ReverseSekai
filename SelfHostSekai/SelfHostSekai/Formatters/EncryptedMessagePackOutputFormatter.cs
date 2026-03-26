using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using MessagePack;
using SelfHostSekai.Cryptography;

namespace SelfHostSekai.Formatters;

public class EncryptedMessagePackOutputFormatter : OutputFormatter
{
    private const string ContentType = "application/octet-stream";
    private readonly AesCryptoHelper _cryptoHelper;

    public EncryptedMessagePackOutputFormatter(AesCryptoHelper cryptoHelper)
    {
        _cryptoHelper = cryptoHelper;
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(ContentType));
    }

    protected override bool CanWriteType(Type? type) => true;

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
    {
        if (context.Object == null)
            return;

        var messagePackBytes = MessagePackSerializer.Serialize(context.Object);
        var encryptedBytes = _cryptoHelper.Encrypt(messagePackBytes);

        var response = context.HttpContext.Response;
        response.ContentType = ContentType;
        await response.Body.WriteAsync(encryptedBytes, 0, encryptedBytes.Length);
    }
}