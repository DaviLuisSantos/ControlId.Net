using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public partial interface IAccessClient
{
    /// <summary>
    /// <c>face_template_match.fcgi</c>: compara um template facial com os cadastrados (V6, V7 e V8).
    /// </summary>
    /// <param name="faceTemplate">Template em Base64 (de <c>face_template_extract</c>); decodificado, tem exatamente 1024 bytes.</param>
    /// <param name="userId">Restringe a verificação a um usuário; <c>null</c> compara com todos (1:N).</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task<FaceTemplateMatchResult> MatchFaceTemplateAsync(string faceTemplate, long? userId = null, CancellationToken cancellationToken = default);

    /// <summary><c>face_template_count_distinct.fcgi</c>: quantos usuários distintos têm template facial (iDFace V6+).</summary>
    Task<int> CountFaceTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>template_match.fcgi</c>: escolhe o melhor template de digital entre várias capturas.
    /// Os templates seguem concatenados no corpo, com <c>temp_num</c> e <c>sizeN</c> na query.
    /// </summary>
    /// <param name="templates">Templates brutos, na ordem; não pode haver item vazio.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    /// <returns>O template escolhido, em Base64.</returns>
    Task<string> MatchFingerprintTemplatesAsync(IReadOnlyList<byte[]> templates, CancellationToken cancellationToken = default);

    /// <summary><c>io_state.fcgi</c>: GPIOs, relés e SecBoxes numa chamada. Apenas iDFace Max V7.x.x.</summary>
    Task<IoState> GetIoStateAsync(CancellationToken cancellationToken = default);

    /// <summary><c>is_identifying.fcgi</c>: há uma identificação facial em andamento. Apenas V6.</summary>
    Task<bool> IsIdentifyingAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_catra_info.fcgi</c>: contadores de giro da catraca. Apenas iDBlock V2 e iDBlock Next IHM.</summary>
    Task<TurnstileCounters> GetTurnstileCountersAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_last_snapshot.fcgi</c>: a última foto da câmera (não dispara captura nova), decodificada do Base64 (V6, V7 e V8).</summary>
    Task<byte[]> GetLastSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary><c>device_rfid_module.fcgi</c>: tipo do módulo leitor RFID (<c>mifare</c>, <c>ASK</c> ou <c>HID</c>).</summary>
    Task<string> GetRfidModuleAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_hid_last_card.fcgi</c>: último cartão lido pelo leitor HID. V7 e V8.</summary>
    Task<HidLastCard> GetHidLastCardAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_wiegand_card_read.fcgi</c>: diagnóstico da última leitura Wiegand (V6, V7 e V8).</summary>
    Task<WiegandCardRead> GetLastWiegandReadAsync(CancellationToken cancellationToken = default);
}
