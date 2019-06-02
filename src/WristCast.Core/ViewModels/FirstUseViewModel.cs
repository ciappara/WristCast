﻿using System.Windows.Input;
using Xamarin.Forms;

namespace WristCast.Core.ViewModels
{
    public class FirstUseViewModel : ViewModel
    {
        private readonly INavigationService _navigationService;
        private readonly ISecretsService _secretsService;

        public FirstUseViewModel(INavigationService navigationService, ISecretsService secretsService)
        {
            _navigationService = navigationService;
            _secretsService = secretsService;
            ApiKey = string.Empty;
            SaveApiKey = new Command(async () =>
              {
                  await _secretsService.SaveSecretAsync(Secrets.KeyAlias,ApiKey);
                  IsApiKeySaved = true;
                  await _navigationService.PopModalAsync();
              });
        }

        public bool IsApiKeyValid => !(string.IsNullOrEmpty(ApiKey) || string.IsNullOrWhiteSpace(ApiKey));

        public bool IsApiKeySaved { get; set; }

        public string TutorialText => "Your key is: " + ApiKey;

        public string ApiKey { get; set; }

        public ICommand SaveApiKey { get; }
    }
}