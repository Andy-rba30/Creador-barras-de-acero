using System;
using System.Collections.Generic;

namespace TiposBarraPeru.Reglas
{
    public enum EstadoFila
    {
        /// <summary>No hay ningun tipo con ese nombre: se creara.</summary>
        SeCreara,
        /// <summary>Ya hay un tipo con ese nombre y no se ha pedido actualizar: se omite.</summary>
        YaExiste,
        /// <summary>Ya hay un tipo con ese nombre y se ha pedido actualizar: se reescriben sus valores.</summary>
        SeActualizara,
        /// <summary>Revit no admite el nombre resultante.</summary>
        NombreInvalido
    }

    /// <summary>Un RebarBarType ya presente en el proyecto (solo lo que necesita el plan).</summary>
    public class TipoExistente
    {
        public string Nombre;
        public double DiametroMm;

        public TipoExistente() { }
        public TipoExistente(string nombre, double diametroMm) { Nombre = nombre; DiametroMm = diametroMm; }
    }

    /// <summary>Una fila de la tabla de la ventana.</summary>
    public class FilaPlan
    {
        public BarraCatalogo Barra;
        /// <summary>Nombre final del tipo (prefijo + nombre de catalogo).</summary>
        public string Nombre;
        public EstadoFila Estado;
        /// <summary>Valor inicial de la casilla "crear" (el usuario puede cambiarlo si el estado lo permite).</summary>
        public bool Crear;
        /// <summary>Tipo existente con ese nombre, si lo hay.</summary>
        public TipoExistente Existente;
        /// <summary>Aviso para la columna de estado (p. ej. el existente tiene otro diametro).</summary>
        public string Aviso;
        public ValoresE060 Valores;

        /// <summary>Texto de la columna "estado".</summary>
        public string TextoEstado
        {
            get
            {
                string t;
                switch (Estado)
                {
                    case EstadoFila.SeCreara: t = "se creara"; break;
                    case EstadoFila.YaExiste: t = "ya existe"; break;
                    case EstadoFila.SeActualizara: t = "se actualizara"; break;
                    default: t = "nombre no valido"; break;
                }
                return string.IsNullOrEmpty(Aviso) ? t : t + " (" + Aviso + ")";
            }
        }

        /// <summary>true si la casilla "crear" puede marcarse con el estado actual.</summary>
        public bool Seleccionable => Estado == EstadoFila.SeCreara || Estado == EstadoFila.SeActualizara;
    }

    /// <summary>
    /// Decide, sin tocar Revit, que se hace con cada entrada del catalogo: comparar el
    /// nombre resultante con los tipos existentes (sin distinguir mayusculas) y marcar
    /// el estado. Idempotente: por defecto solo se crean los que faltan.
    /// </summary>
    public static class Planificador
    {
        /// <param name="cfg">Configuracion (catalogo, reglas, simbolo de pulgada, tolerancia).</param>
        /// <param name="prefijo">Prefijo de nombre elegido en la ventana.</param>
        /// <param name="existentes">Tipos de barra del proyecto.</param>
        /// <param name="actualizarExistentes">true = los existentes se reescriben en vez de omitirse.</param>
        /// <param name="nombreValido">Validador de nombres de Revit (NamingUtils.IsValidName); null = todos validos.</param>
        public static List<FilaPlan> Planificar(Configuracion cfg, string prefijo, IEnumerable<TipoExistente> existentes,
                                                bool actualizarExistentes, Func<string, bool> nombreValido = null)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var reglas = new ReglasE060(cfg.ReglasE060);
            var lista = new List<TipoExistente>(existentes ?? new TipoExistente[0]);
            var filas = new List<FilaPlan>();

            foreach (BarraCatalogo barra in cfg.Catalogo)
            {
                var fila = new FilaPlan
                {
                    Barra = barra,
                    Nombre = Nombres.Generar(prefijo, barra.Nombre, cfg.SimboloPulgada),
                    Valores = reglas.Calcular(barra.DiametroMm)
                };
                fila.Existente = lista.Find(e => e != null && Nombres.Iguales(e.Nombre, fila.Nombre));

                if (nombreValido != null && !nombreValido(fila.Nombre))
                {
                    fila.Estado = EstadoFila.NombreInvalido;
                    fila.Crear = false;
                }
                else if (fila.Existente == null)
                {
                    fila.Estado = EstadoFila.SeCreara;
                    fila.Crear = true;
                }
                else
                {
                    fila.Estado = actualizarExistentes ? EstadoFila.SeActualizara : EstadoFila.YaExiste;
                    fila.Crear = actualizarExistentes;
                    if (Math.Abs(fila.Existente.DiametroMm - barra.DiametroMm) > cfg.ToleranciaDiametroMm)
                        fila.Aviso = "con diametro " + fila.Existente.DiametroMm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " mm";
                }
                filas.Add(fila);
            }
            return filas;
        }
    }
}
