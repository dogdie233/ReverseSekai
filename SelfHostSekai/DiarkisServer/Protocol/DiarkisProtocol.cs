using System.Security.Cryptography;
using MessagePack;

namespace DiarkisServer.Protocol;

/// <summary>
/// Binary protocol compatible with Diarkis SDK.
/// Packet layout: [ver:4][cmd:4][status:4][payloadSize:4][payload:N]
/// All little-endian. Payload is optionally AES-CBC encrypted + HMAC-SHA256 signed.
/// </summary>
public static class DiarkisProtocol
{
    public const int HeaderSize = 16;
    public const int HmacLength = 32;
    public const int AesBlockSize = 16;

    // ── Status codes ──
    public const uint StatusOk  = 1;
    public const uint StatusBad = 4;
    public const uint StatusErr = 5;

    // ── Diarkis built-in utility commands (ver=0) ──
    public const uint UtilVer         = 0;
    public const uint EchoCmd         = 1;
    public const uint HeartbeatCmd    = 1;
    public const uint ReconnCmd       = 2;
    public const uint PingCmd         = 3;
    public const uint ClientKeyCmd    = 4;
    public const uint ReconnWTypeCmd  = 5;
    public const uint NotificationCmd = 400;

    // ── Diarkis Room module commands ──
    public const uint RoomCreate      = 100;
    public const uint RoomJoin        = 101;
    public const uint RoomLeave       = 102;
    public const uint RoomBroadcast   = 103;
    public const uint RoomMessage     = 104;
    public const uint RoomRandCreate  = 105;
    public const uint RoomRandJoin    = 106;
    public const uint RoomUpdateProp  = 107;
    public const uint RoomGetProp     = 108;
    public const uint RoomGetOwner    = 109;
    public const uint RoomIncrProp    = 10;
    public const uint RoomGetMembers  = 11;
    public const uint RoomMigrate     = 12;
    public const uint RoomGetNumMem   = 13;
    public const uint RoomOwnerChange = 14;
    public const uint RoomRegister    = 115;
    public const uint RoomFindRooms   = 116;
    public const uint RoomReserve     = 117;
    public const uint RoomCancelRes   = 118;
    public const uint RoomChat        = 125;
    public const uint RoomChatLog     = 126;
    public const uint RoomP2PInit     = 127;
    public const uint RoomObjSync     = 128;
    public const uint RoomObjUpdate   = 129;
    public const uint RoomPropSync    = 130;
    public const uint RoomRelay       = 18;
    public const uint RoomRelayProf   = 19;

    // ── Diarkis MatchMaker commands ──
    public const uint MatchWait       = 200;
    public const uint MatchSearch     = 201;
    public const uint MatchRemove     = 202;
    public const uint MatchLeave      = 203;
    public const uint MatchSync       = 204;
    public const uint MatchClaim      = 205;
    public const uint MatchComplete   = 206;
    public const uint MatchResults    = 207;

    // ── Diarkis Group commands ──
    public const uint GroupCreate     = 110;
    public const uint GroupJoin       = 111;
    public const uint GroupLeave      = 112;
    public const uint GroupBroadcast  = 113;
    public const uint GroupRandJoin   = 114;

    // ── CP.Realtime custom commands ──
    public const uint CustomRandJoin           = 1010;
    public const uint CustomRandRoomJoin       = 10111;
    public const uint PlayerInfo               = 1011;
    public const uint RoomSync                 = 1012;
    public const uint RoomSyncMinimal          = 1022;
    public const uint CloseMatchmake           = 1013;
    public const uint CloseRoom                = 1024;
    public const uint ChangeMatchmakeCondition = 1014;
    public const uint AddMatchmakeCondition    = 1034;
    public const uint OpenRoom                 = 1023;
    public const uint ScaleUpMatchmake         = 1015;
    public const uint ResetScaleUp             = 1025;
    public const uint MatchingMoveAndScaleUp   = 1035;
    public const uint CustomCreate             = 1016;
    public const uint UpdateRoomProperty       = 10010;
    public const uint UpdatePlayerProperty     = 10020;
    public const uint UpdatePlayerPropAndIdx   = 10030;
    public const uint CustomJoin               = 1019;
    public const uint UnlockJoin               = 1049;
    public const uint JoinPostProcess          = 1032;
    public const uint JoinPostProcessMinimal   = 1042;
    public const uint RefreshUserIndex         = 1021;
    public const uint ReleasePrivateRoom       = 2000;
    public const uint MatchingRoomMove         = 1020;
    public const uint DirectRoomMove           = 1029;
    public const uint ForceDirectRoomMove      = 1059;
    public const uint CustomRecreate           = 1300;
    public const uint TimestampCmd             = 10000;
    public const uint ChangeOwner              = 998;
    public const uint RoomMemberCmd            = 600;

    // ── MultiLive game-layer commands ──
    public const uint MultiLiveRandJoin              = 3000;
    public const uint MultiLiveRandRoomJoin          = 3001;
    public const uint MultiLiveCloseMatchmake        = 3030;
    public const uint MultiLiveAddMatchmakeCondition = 3040;
    public const uint MultiLiveScaleUpMatchmake      = 3050;
    public const uint MultiLiveCustomCreate          = 3060;
    public const uint MultiLiveCreate                = 3070;
    public const uint MultiLiveCustomJoin            = 3080;
    public const uint ReleaseMultiLivePrivateRoom    = 3090;
    public const uint MultiLivePrivateCloseMatchmake = 3100;
    public const uint MultiLiveReStartPrivate        = 3110;
    public const uint MultiLiveUnlockJoin            = 3120;
    public const uint UpdateTotalPowerLimit          = 10040;

    // ── Packet encode/decode ──

    public record Packet(uint Ver, uint Cmd, uint Status, byte[]? Payload);

    public static Packet Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
            throw new InvalidDataException($"Packet too short: {data.Length}");

        var ver  = BitConverter.ToUInt32(data[..4]);
        var cmd  = BitConverter.ToUInt32(data[4..8]);
        var status = BitConverter.ToUInt32(data[8..12]);
        var payloadSize = BitConverter.ToInt32(data[12..16]);

        byte[]? payload = null;
        if (payloadSize > 0 && data.Length >= HeaderSize + payloadSize)
            payload = data.Slice(HeaderSize, payloadSize).ToArray();

        return new Packet(ver, cmd, status, payload);
    }

    public static byte[] Encode(uint ver, uint cmd, uint status, byte[]? payload = null)
    {
        var len = payload?.Length ?? 0;
        var buf = new byte[HeaderSize + len];
        BitConverter.TryWriteBytes(buf.AsSpan(0, 4), ver);
        BitConverter.TryWriteBytes(buf.AsSpan(4, 4), cmd);
        BitConverter.TryWriteBytes(buf.AsSpan(8, 4), status);
        BitConverter.TryWriteBytes(buf.AsSpan(12, 4), len);
        payload?.CopyTo(buf, HeaderSize);
        return buf;
    }

    public static byte[] EncodeOk(uint cmd, object? body = null)
    {
        byte[]? payload = body != null ? MessagePackSerializer.Serialize(body.GetType(), body) : null;
        return Encode(2, cmd, StatusOk, payload);
    }

    public static byte[] EncodeError(uint cmd)
        => Encode(2, cmd, StatusErr);

    // ── Diarkis encryption: AES-CBC + HMAC-SHA256 ──

    public static byte[] EncryptAndSign(byte[] key, byte[] iv, byte[] macKey, byte[] plaintext)
    {
        // Encrypt
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        byte[] ciphertext;
        using (var enc = aes.CreateEncryptor())
            ciphertext = enc.TransformFinalBlock(plaintext, 0, plaintext.Length);

        // Sign
        using var hmac = new HMACSHA256(macKey);
        var mac = hmac.ComputeHash(ciphertext);

        // [ciphertext][mac(32)]
        var result = new byte[ciphertext.Length + HmacLength];
        ciphertext.CopyTo(result, 0);
        mac.CopyTo(result, ciphertext.Length);
        return result;
    }

    public static byte[]? AuthAndDecrypt(byte[] key, byte[] iv, byte[] macKey, byte[] data)
    {
        if (data.Length < HmacLength)
            return null;

        var cipherLen = data.Length - HmacLength;
        var ciphertext = data.AsSpan(0, cipherLen);
        var receivedMac = data.AsSpan(cipherLen, HmacLength);

        // Verify HMAC
        using var hmac = new HMACSHA256(macKey);
        var computedMac = hmac.ComputeHash(data, 0, cipherLen);
        if (!CryptographicOperations.FixedTimeEquals(computedMac, receivedMac))
            return null;

        // Decrypt
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(data, 0, cipherLen);
    }
}
