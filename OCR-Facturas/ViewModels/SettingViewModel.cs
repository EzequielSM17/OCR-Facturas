using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCR_Facturas.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace OCR_Facturas.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly AppSettings _appSettings;

        [ObservableProperty]
        private string _azureEndpoint;

        [ObservableProperty]
        private string _azureKey;

        [ObservableProperty]
        private string _postgresConnectionString;

        public SettingsViewModel(AppSettings appSettings)
        {
            _appSettings = appSettings;
            LoadSettingsAsync(); // Cargamos al iniciar la vista
        }

        private async void LoadSettingsAsync()
        {
            // 1. Intentamos recuperar los datos guardados en el dispositivo
            var savedEndpoint = Preferences.Get("AzureEndpoint", string.Empty);
            var savedKey = await SecureStorage.GetAsync("AzureKey");
            var savedConn = await SecureStorage.GetAsync("PostgresConn");

            // 2. Si no hay nada guardado (primera vez), usamos los valores por defecto del Singleton
            AzureEndpoint = string.IsNullOrEmpty(savedEndpoint) ? _appSettings.AzureEndpoint : savedEndpoint;
            AzureKey = string.IsNullOrEmpty(savedKey) ? _appSettings.AzureKey : savedKey;
            PostgresConnectionString = string.IsNullOrEmpty(savedConn) ? _appSettings.PostgresConnectionString : savedConn;

            // 3. Sincronizamos el Singleton por si recuperamos datos guardados
            _appSettings.AzureEndpoint = AzureEndpoint;
            _appSettings.AzureKey = AzureKey;
            _appSettings.PostgresConnectionString = PostgresConnectionString;
        }

        [RelayCommand]
        public async Task SaveSettingsAsync()
        {
            // Guardar en el dispositivo
            Preferences.Set("AzureEndpoint", AzureEndpoint);
            await SecureStorage.SetAsync("AzureKey", AzureKey);
            await SecureStorage.SetAsync("PostgresConn", PostgresConnectionString);

            // Actualizar el Singleton en memoria para que los servicios lo usen de inmediato
            _appSettings.AzureEndpoint = AzureEndpoint;
            _appSettings.AzureKey = AzureKey;
            _appSettings.PostgresConnectionString = PostgresConnectionString;

            // Usar MainThread por si acaso para la alerta
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Application.Current.MainPage.DisplayAlert("Configuración", "Credenciales guardadas correctamente.", "OK");
                await Shell.Current.GoToAsync("//MainPage");
            });
            
        }
        [RelayCommand]
        public async Task CloseAsync()
        {
            // Esto "cierra" el modal actual y te devuelve automáticamente a la MainPage que estaba debajo
            if (Application.Current?.MainPage != null)
            {
                await Shell.Current.GoToAsync("//MainPage");
            }
        }
    }
}
