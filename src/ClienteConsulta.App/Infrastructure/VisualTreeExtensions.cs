using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ClienteConsulta.App.Infrastructure;

public static class VisualTreeExtensions
{
    public static T? FindAncestor<T>(this DependencyObject? source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match) return match;

            // Run/Span/Bold etc. (elementos de texto dentro de um TextBlock, ex. os <Run>
            // do subtítulo da lista) não são Visual/Visual3D — VisualTreeHelper.GetParent
            // lança "não é um visual ou visual3d" nesses nós. Um clique de mouse pode ter
            // e.OriginalSource apontando pra um deles (teclado nunca passa por aqui, por
            // isso o erro só aparecia usando o mouse). LogicalTreeHelper.GetParent cobre
            // esses casos; para nós normais da árvore visual, sobe pelo visual mesmo.
            current = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
