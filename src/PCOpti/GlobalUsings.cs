// WPF projects leave System.IO out of the implicit usings (it clashes with System.Windows.Shapes.Path).
// PCOpti never uses Shapes.Path in code, so we add it back globally.
global using System.IO;
