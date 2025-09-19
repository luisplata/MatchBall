using System.Collections.Generic;
using System.Linq;
using App.Ports;
using Cysharp.Threading.Tasks;
using Domain.Events;

namespace App.UseCase.PowerUps
{
    public class PowerUpsUseCase : IPowerUpsUseCase
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly HashSet<string> _powerUpsSelected = new();
        private List<PowerUpData> _powerUpsData = new();
        private readonly IEventBus _eventBus;

        public PowerUpsUseCase(IInventoryRepository inventoryRepository,
            IEventBus eventBus)
        {
            _inventoryRepository = inventoryRepository;
            _eventBus = eventBus;

            _eventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
        }

        private void OnCurrencyChanged(CurrencyChangedEvent obj)
        {
            LoadPowerUps().Forget();
        }

        /// <summary>
        /// Carga los PowerUps desde el repositorio y sincroniza su estado seleccionado.
        /// </summary>
        public async UniTask LoadPowerUps()
        {
            var inventory = await _inventoryRepository.GetPlayerInventory();

            _powerUpsData = inventory.PowerUps
                .Where(p => p.Quantity > 0)
                .Select(p => new PowerUpData
                {
                    id = p.Id,
                    selected = _powerUpsSelected.Contains(p.Id),
                    quantity = p.Quantity
                })
                .ToList();

            SyncPowerUps();
        }

        /// <summary>
        /// Marca o desmarca un PowerUp como seleccionado.
        /// </summary>
        public bool PowerUpSelected(string powerUpId, bool isSelected)
        {
            if (isSelected)
                _powerUpsSelected.Add(powerUpId);
            else
                _powerUpsSelected.Remove(powerUpId);

            SyncPowerUps();
            return isSelected;
        }

        /// <summary>
        /// Devuelve todos los PowerUps cargados.
        /// </summary>
        public IReadOnlyList<PowerUpData> GetPowerUps() => _powerUpsData;

        /// <summary>
        /// Devuelve los PowerUps que están actualmente seleccionados.
        /// </summary>
        public IReadOnlyList<PowerUpData> GetSelectedPowerUps() =>
            _powerUpsData.Where(p => p.selected).ToList();

        /// <summary>
        /// Consume un PowerUp y recarga el inventario.
        /// </summary>
        public async UniTask ConsumePowerUp(string id)
        {
            await _inventoryRepository.ConsumePowerUp(id, 1);
            await LoadPowerUps();
        }

        /// <summary>
        /// Sincroniza el flag "selected" de los PowerUpsData con la lista de seleccionados.
        /// </summary>
        private void SyncPowerUps()
        {
            foreach (var powerUp in _powerUpsData)
            {
                powerUp.selected = _powerUpsSelected.Contains(powerUp.id);
            }

            // Asegura consistencia
            _powerUpsSelected.Clear();
            foreach (var p in _powerUpsData.Where(p => p.selected))
                _powerUpsSelected.Add(p.id);
        }
    }
}