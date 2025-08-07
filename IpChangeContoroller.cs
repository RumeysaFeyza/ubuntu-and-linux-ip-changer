using Microsoft.AspNetCore.Mvc;
using Renci.SshNet;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace UbuntuIpManager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IpController : ControllerBase
    {
        [HttpPost("change")]
        [SwaggerOperation(Summary = "Ubuntu sanal makinenin IP adresini değiştirir.")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public IActionResult ChangeIp([FromBody, Required] IpChangeRequest request)
        {
            if (request == null)
                return BadRequest("İstek verisi boş.");

            if (string.IsNullOrWhiteSpace(request.UbuntuIp) ||
                string.IsNullOrWhiteSpace(request.SshUsername) ||
                string.IsNullOrWhiteSpace(request.SshPassword) ||
                string.IsNullOrWhiteSpace(request.NewIp))
            {
                return BadRequest("Tüm alanlar doldurulmalıdır.");
            }

            try
            {
                using (var ping = new System.Net.NetworkInformation.Ping())
                {
                    var reply = ping.Send(request.NewIp, 1000); // 1 saniye timeout
                    if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                    {
                        return BadRequest($"Yeni IP adresi ({request.NewIp}) zaten başka bir cihaz tarafından kullanılıyor.");
                    }
                }
            }
            catch (Exception pingEx)
            {

            }

            try
            {
                using (var client = new SshClient(request.UbuntuIp, request.SshUsername, request.SshPassword))
                {
                    client.Connect();
                    if (!client.IsConnected)
                        return StatusCode(500, "SSH bağlantısı başarısız.");

                    // IP yapılandırmasını güncelle
                    string changeIpCommand = $@"
echo 'network:
  version: 2
  ethernets:
    eth0:
      dhcp4: no
      addresses: [{request.NewIp}/24]
      gateway4: 192.168.1.1
      nameservers:
        addresses: [8.8.8.8,8.8.4.4]' | sudo tee /etc/netplan/01-netcfg.yaml
";

                    // Komutları temizle ve uygula
                    changeIpCommand = changeIpCommand.Replace("\r", "");

                    var cmd1 = client.RunCommand(changeIpCommand);
                    if (!string.IsNullOrWhiteSpace(cmd1.Error))
                        return StatusCode(500, $"IP dosyası yazma hatası: {cmd1.Error}");

                    // Netplan apply komutu
                    var cmd2 = client.RunCommand("sudo netplan apply");
                    if (!string.IsNullOrWhiteSpace(cmd2.Error))
                        return StatusCode(500, $"Netplan uygulama hatası: {cmd2.Error}");

                    client.Disconnect();
                }

                return Ok("IP adresi başarıyla değiştirildi.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Sunucu hatası: {ex.Message}");
            }
        }
    }

    public class IpChangeRequest
    {
        [Required]
        [SwaggerSchema(Description = "Ubuntu sanal makinenin mevcut IP adresi")]
        public string UbuntuIp { get; set; }

        [Required]
        [SwaggerSchema(Description = "SSH bağlantısı için kullanıcı adı")]
        public string SshUsername { get; set; }

        [Required]
        [SwaggerSchema(Description = "SSH bağlantısı için şifre")]
        public string SshPassword { get; set; }

        [Required]
        [SwaggerSchema(Description = "Yeni atanacak IP adresi")]
        public string NewIp { get; set; }
    }
}
