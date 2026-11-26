using System;
using System.Collections.Generic;

namespace ControlId.Access;

/// <summary>
/// Lista de imagens guardadas no equipamento: <c>get_slideshow_images_list.fcgi</c> (slideshow) e
/// <c>get_kiosk_icons_list.fcgi</c> (ícones do modo quiosque).
/// </summary>
public sealed class ScreenImageList
{
    /// <summary>Cria a lista.</summary>
    /// <param name="filenames">Nomes dos arquivos guardados.</param>
    /// <param name="maxFiles">Máximo de arquivos que o equipamento aceita.</param>
    public ScreenImageList(IReadOnlyList<string> filenames, int maxFiles)
    {
        Filenames = filenames ?? throw new ArgumentNullException(nameof(filenames));
        MaxFiles = maxFiles;
    }

    /// <summary>Nomes dos arquivos (<c>filename</c> de cada item de <c>images</c>/<c>icons</c>).</summary>
    public IReadOnlyList<string> Filenames { get; }

    /// <summary>Máximo de arquivos (<c>max_files</c>; no quiosque, sempre 4).</summary>
    public int MaxFiles { get; }
}
