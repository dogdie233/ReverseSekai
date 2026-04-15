namespace SelfHostSekai.Configuration;

public class DiarkisOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8000;
    public int UdpPort { get; set; } = 8001;
    public string EncryptionAlgorithm { get; set; } = "AES-256-GCM";
}
