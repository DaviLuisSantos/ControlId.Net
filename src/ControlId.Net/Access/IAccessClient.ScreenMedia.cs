using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public partial interface IAccessClient
{
    /// <summary>
    /// <c>send_slideshow_images.fcgi</c>: envia uma imagem JPEG/JPG para o slideshow.
    /// </summary>
    /// <param name="filename">Nome com que o arquivo fica no equipamento.</param>
    /// <param name="jpeg">Bytes da imagem JPEG (não pode estar vazia).</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task SendSlideshowImageAsync(string filename, byte[] jpeg, CancellationToken cancellationToken = default);

    /// <summary><c>get_slideshow_images_list.fcgi</c>: lista as imagens do slideshow e o máximo aceito.</summary>
    Task<ScreenImageList> GetSlideshowImagesAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_slideshow_image.fcgi</c>: baixa o arquivo de uma imagem do slideshow.</summary>
    /// <param name="filename">Nome do arquivo.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task<byte[]> GetSlideshowImageAsync(string filename, CancellationToken cancellationToken = default);

    /// <summary><c>delete_slideshow_image.fcgi</c>: remove uma imagem do slideshow.</summary>
    /// <param name="filename">Nome do arquivo.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task DeleteSlideshowImageAsync(string filename, CancellationToken cancellationToken = default);

    /// <summary><c>remove_slideshow_images.fcgi</c>: remove todas as imagens do slideshow.</summary>
    /// <param name="confirmation">Confirmação de que as imagens apagadas não voltam.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task RemoveAllSlideshowImagesAsync(DataLossConfirmation confirmation, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_kiosk_icon.fcgi</c>: envia um ícone do modo quiosque (iDFace Max V7 e iDFace/iDFace Max V8).
    /// </summary>
    /// <param name="filename">Nome do arquivo; a extensão precisa ser <c>.png</c>, <c>.jpg</c>, <c>.jpeg</c> ou <c>.svg</c>.</param>
    /// <param name="image">Bytes do ícone (não pode estar vazio).</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task SetKioskIconAsync(string filename, byte[] image, CancellationToken cancellationToken = default);

    /// <summary><c>get_kiosk_icon.fcgi</c>: baixa um ícone do modo quiosque.</summary>
    /// <param name="filename">Nome do arquivo.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task<byte[]> GetKioskIconAsync(string filename, CancellationToken cancellationToken = default);

    /// <summary><c>delete_kiosk_icon.fcgi</c>: remove um ícone do modo quiosque.</summary>
    /// <param name="filename">Nome do arquivo.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task DeleteKioskIconAsync(string filename, CancellationToken cancellationToken = default);

    /// <summary><c>get_kiosk_icons_list.fcgi</c>: lista os ícones do modo quiosque (no máximo 4).</summary>
    Task<ScreenImageList> GetKioskIconsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>set_phone_icon.fcgi</c>: envia o ícone PNG exibido na tela durante chamadas SIP (iDFace).</summary>
    /// <param name="png">Bytes do PNG.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task SetPhoneIconAsync(byte[] png, CancellationToken cancellationToken = default);

    /// <summary><c>get_phone_icon.fcgi</c>: baixa o ícone PNG da interfonia; o equipamento dá erro se não houver ícone personalizado.</summary>
    Task<byte[]> GetPhoneIconAsync(CancellationToken cancellationToken = default);

    /// <summary><c>remove_phone_icon.fcgi</c>: remove o ícone personalizado da interfonia e volta ao padrão.</summary>
    Task RemovePhoneIconAsync(CancellationToken cancellationToken = default);

    /// <summary><c>make_sip_conference.fcgi</c>: inicia uma chamada de conferência SIP (firmware V6, V7 e V8).</summary>
    /// <param name="target">Ramal SIP de destino; <c>null</c> ou vazio usa o destino padrão configurado.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task MakeSipConferenceAsync(string? target = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_streaming_logo.fcgi</c>: envia o logotipo PNG sobreposto ao streaming de vídeo (iDFace).
    /// A doc recomenda proporção 5:1, menos de 1 MB e largura e altura abaixo de 1000 pixels.
    /// </summary>
    /// <param name="png">Bytes do PNG (menor que 1 MB).</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task SetStreamingLogoAsync(byte[] png, CancellationToken cancellationToken = default);

    /// <summary><c>get_streaming_logo.fcgi</c>: baixa o logotipo PNG do streaming; o equipamento dá erro se não houver logotipo personalizado.</summary>
    Task<byte[]> GetStreamingLogoAsync(CancellationToken cancellationToken = default);

    /// <summary><c>remove_streaming_logo.fcgi</c>: remove o logotipo personalizado do streaming.</summary>
    Task RemoveStreamingLogoAsync(CancellationToken cancellationToken = default);
}
