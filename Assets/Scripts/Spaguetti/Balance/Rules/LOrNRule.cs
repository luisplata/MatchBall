using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace Spaguetti.Balance.Rules
{
    /// <summary>
    /// Regla para detectar formas en L o N: cadenas verticales (>= 2) en un lado
    /// con al menos una coincidencia horizontal en el otro lado.
    /// - Si detecta una cadena vertical de N bolas del mismo tipo en un lado
    ///   y al menos una de esas posiciones tiene una bola del mismo tipo en el
    ///   lado opuesto, elimina las bolas involucradas y desplaza las demás.
    /// - Calcula el desplazamiento como: diff = 2 * numeroDeBolasVerticalesEncontradas
    /// - Usa el índice mínimo de las posiciones afectadas como "start index" para MoveBallsDownByIndexAsc
    ///
    /// Asunciones razonables:
    /// - Las posiciones verticales están separadas por un "step" de 2 (consistente con otras reglas).
    /// - El chequeo se realiza respecto al índice provisto por el iterador de reglas; casos que no incluyan
    ///   ese índice serán evaluados cuando el iterador llame con otro índice.
    /// - Se otorga un único AddPoints() por aplicación de regla (igual que otras reglas existentes).
    /// </summary>
    public class LOrNRule : ICombinationRule
    {
        private readonly IMusicController _music;
        private readonly IDelayProvider _delay;
        private const int VerticalStep = 2;

        public LOrNRule(IMusicController music, IDelayProvider delay)
        {
            _music = music;
            _delay = delay;
        }

        public async UniTask<bool> TryApplyAsync(IBalance balance, int index)
        {
            var left = balance.GetLeftPath();
            var right = balance.GetRightPath();

            // Intentar detectar cadena vertical en la izquierda y match horizontal en la derecha
            if (await TryApplyVerticalHorizontal(balance, left, right, index))
                return true;

            // Intentar detectar cadena vertical en la derecha y match horizontal en la izquierda
            if (await TryApplyVerticalHorizontal(balance, right, left, index))
                return true;

            return false;
        }

        private async UniTask<bool> TryApplyVerticalHorizontal(IBalance balance, Path verticalPath, Path otherPath, int index)
        {
            // Necesitamos una bola base en el índice proporcionado para empezar la expansión
            if (!verticalPath.TryGetBallInPosition(index, out var baseBall))
                return false;

            // Limite del array de bolas: una expansión que lo exceda significa "no hay más coincidencias"
            var columns = balance.CountOfColumns();
            var id = baseBall.GetId();

            // Construir la cadena vertical que contenga el índice: hacia arriba (+step) y hacia abajo (-step)
            var indices = new List<int> { index };

            // Hacia arriba
            var cur = index;
            while (cur + VerticalStep < columns &&
                   verticalPath.TryGetBallInPosition(cur + VerticalStep, out var upBall) &&
                   MatchHelper.Matches(upBall.GetId(), id))
            {
                cur += VerticalStep;
                indices.Add(cur);
            }

            // Hacia abajo
            cur = index;
            while (cur - VerticalStep >= 0 &&
                   verticalPath.TryGetBallInPosition(cur - VerticalStep, out var downBall) &&
                   MatchHelper.Matches(downBall.GetId(), id))
            {
                cur -= VerticalStep;
                indices.Insert(0, cur);
            }

            // Debe haber al menos 2 bolas en la cadena vertical para considerar L/N
            if (indices.Count < 2)
                return false;

            // Buscar coincidencias horizontales en el otro path dentro de las posiciones de la cadena
            var otherMatchedIndices = new List<int>();
            foreach (var i in indices)
            {
                if (otherPath.TryGetBallInPosition(i, out var ob) && MatchHelper.Matches(ob.GetId(), id))
                {
                    otherMatchedIndices.Add(i);
                }
            }

            // Es necesario que exista al menos una coincidencia horizontal
            if (otherMatchedIndices.Count == 0)
                return false;

            // Expandir verticalmente en el otro path alrededor de cada coincidencia encontrada
            // para incluir bolas adyacentes del mismo tipo (cubriendo el caso descrito por el usuario).
            var otherSet = new HashSet<int>(otherMatchedIndices);
            foreach (var start in otherMatchedIndices.ToList())
            {
                // Hacia arriba en otherPath
                var curO = start;
                while (curO + VerticalStep < columns &&
                       otherPath.TryGetBallInPosition(curO + VerticalStep, out var upOb) &&
                       MatchHelper.Matches(upOb.GetId(), id))
                {
                    curO += VerticalStep;
                    otherSet.Add(curO);
                }

                // Hacia abajo en otherPath
                curO = start;
                while (curO - VerticalStep >= 0 &&
                       otherPath.TryGetBallInPosition(curO - VerticalStep, out var downOb) &&
                       MatchHelper.Matches(downOb.GetId(), id))
                {
                    curO -= VerticalStep;
                    otherSet.Add(curO);
                }
            }

            otherMatchedIndices = otherSet.OrderBy(x => x).ToList();

            // ---- Aplicar el borrado ----
            // Remover todas las bolas de la cadena vertical en el path vertical
            foreach (var i in indices)
            {
                verticalPath.RemoveBall(i);
            }

            // Remover en el otro path solamente las posiciones que coinciden
            foreach (var i in otherMatchedIndices)
            {
                otherPath.RemoveBall(i);
            }

            _music?.PlaySfx("pop");

            var verticalCount = indices.Count;
            var verticalDiff = 2 * verticalCount; // según especificación del usuario
            var verticalMinIndex = indices.Min();

            var otherCount = otherMatchedIndices.Count;
            var otherDiff = 2 * otherCount;
            var otherMinIndex = otherMatchedIndices.Min();

            // Mover ambas rutas simultáneamente: cada path baja las bolas que se eliminaron en él
            await UniTask.WhenAll(verticalPath.MoveBallsDownByIndexAsc(verticalDiff, verticalMinIndex),
                otherPath.MoveBallsDownByIndexAsc(otherDiff, otherMinIndex));

            await _delay.DelaySeconds(.5f);
            balance.AddPoints();
            return true;
        }
    }
}
