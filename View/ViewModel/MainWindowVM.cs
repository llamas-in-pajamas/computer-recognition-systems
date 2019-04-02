﻿using BespokeFusion;
using ClassificationServices;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Services;
using SGMParser;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using View.ViewModel.Base;


namespace View.ViewModel
{
    public class MainWindowVM : BaseVM
    {
        #region private fields

        #region Theme related fields
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        private const string RegistryValueName = "AppsUseLightTheme";

        private bool _isDarkTheme = true;

        private Brush _foreground;
        private Brush _background;
        #endregion

        private List<ArticleModel> _articles;
        private List<ArticleModel> _filteredOutArticles;
        private List<ArticleModel> _learningArticles;
        private List<ClassificationModel> _trainingArticles;
        private List<string> _stopList;
        private string _categoryComboboxSelected;
        private string _keyWordsMethodSelected;
        private Dictionary<string, List<string>> _keyWords;

        private string _errorMessage;
        private string _currentExpanded;

        #endregion
        public ICommand SelectCatalogButton { get; }
        public ICommand SelectStopListButton { get; }
        public ICommand SelectDefaultButton { get; }
        public ICommand PreProcessButton { get; }

        #region props

        public bool IsEnabledStopListBTN { get; set; }
        public string SelectArticleText { get; set; }
        public string SelectStopListText { get; set; }
        public bool IsArticleLoadingVisible { get; set; }
        public bool NumOfArticlesVisibility { get; set; }
        public bool NumOfStopListVisibility { get; set; }


        public int NumberOfLearningArticlesTB { get; set; }
        public int NumberOfTrainingArticlesTB { get; set; }

        public string CurrentExpanded
        {
            get => _currentExpanded;
            set
            {
                if (_currentExpanded == value)
                {
                    _currentExpanded = ExpandedValues.None;

                }
                else
                {
                    _currentExpanded = value;
                }
                OnPropertyChanged(nameof(CurrentExpanded));

            }
        }

        public bool KeyWordsIsEnabled { get; set; }
        public ObservableCollection<string> CategoryCombobox { get; set; }
        public string CategoryComboboxSelected
        {
            get => _categoryComboboxSelected;
            set
            {
                OnPropertyChanged(nameof(CategoryComboboxSelected));
                _categoryComboboxSelected = value;
                LoadCategoryItems(value, _articles);
            }
        }
        public ObservableCollection<SelectableVM> CategoryItems { get; set; }
        public ObservableCollection<KeyWordsVM> KeyWordsItems { get; set; }

        public int AmountLearningDataSlider { get; set; } = 60;
        public bool IsEnabledPreProcessBTN { get; set; }
        public bool PreprocessDataProgressVisibility { get; set; }
        public int NumberOfKeyWordsTB { get; set; } = 8;
        public ObservableCollection<string> KeyWordsMethodCombobox { get; set; } = new ObservableCollection<string>()
        {
            "1. Term Frequency",
            "2. Extended Term Frequency"
        };
        public string KeyWordsMethodSelected
        {
            get => _keyWordsMethodSelected;
            set
            {
                _keyWordsMethodSelected = value;
                KeyWordsFilterWordsVisibility = value.Substring(0, 1) == "2";
                OnPropertyChanged(nameof(KeyWordsMethodSelected));


            }
        }

        public bool KeyWordsFilterWordsVisibility { get; set; }
        public int KeyWordsFilterWordsTB { get; set; } = 100;

        #endregion


        public MainWindowVM()
        {
            SelectCatalogButton = new RelayCommand(LoadArticlesFromCatalog);
            SelectStopListButton = new RelayCommand(LoadStopList);
            SelectDefaultButton = new RelayCommand(LoadDefaultValues);
            PreProcessButton = new RelayCommand(PreProcessData);
            ReadWindowsSetting();
            ApplyBase(_isDarkTheme);
            ApplyColors(_isDarkTheme);
        }

        #region Theme Solvers

        private void ReadWindowsSetting()
        {
            //            var uiSettings = SystemParameters.WindowGlassBrush;
            //            var uiSettings1 = SystemParameters.WindowGlassColor;

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
            {
                object registryValueObject = key?.GetValue(RegistryValueName);
                if (registryValueObject == null)
                {
                    _isDarkTheme = false;
                }

                int registryValue = (int)registryValueObject;


                if (registryValue > 0)
                {
                    _isDarkTheme = false;
                }
                else
                {
                    _isDarkTheme = true;
                }
            }
        }

        private void ApplyBase(bool isDark)
        {
            new PaletteHelper().SetLightDark(isDark);
        }

        private void ApplyAccent(Swatch swatch)
        {
            new PaletteHelper().ReplaceAccentColor(swatch);
        }

        private void ApplyColors(bool isDark)
        {
            if (isDark)
            {
                _foreground = Brushes.White;
                _background = (SolidColorBrush)new BrushConverter().ConvertFrom("#303030");
                return;
            }

            _foreground = Brushes.Black;
            _background = Brushes.White;
        }


        #endregion


        #region Pre-process Data Methods  
        //TODO: Finish pre-processing articles
        private async void PreProcessData()
        {
            if (string.IsNullOrEmpty(KeyWordsMethodSelected))
            {
                ShowErrorMaterial("You need to select key-words extraction method");
                return;
            }
            PreprocessDataProgressVisibility = true;
            CurrentExpanded = ExpandedValues.None;
            KeyWordsIsEnabled = false;
            bool success = false;
            double time = 0;
            Stopwatch timer = new Stopwatch();
            await Task.Run(() =>
            {
                try
                {
                    FilterArticles(_articles, CategoryItems.ToList(), CategoryComboboxSelected);
                    SplitArticles(AmountLearningDataSlider);
                    NumberOfLearningArticlesTB = _learningArticles.Count;
                    NumberOfTrainingArticlesTB = _trainingArticles.Count;
                    timer.Start();

                    ExtractKeyWords();

                    timer.Stop();
                    time = timer.ElapsedMilliseconds;
                    success = true;
                }
                catch (Exception e)
                {
                    timer.Stop();
                    _errorMessage = e.Message;
                }
            }
            );
            if (!success)
            {
                ShowErrors();

            }
            else
            {
                LoadKeyWordsToGui();

                KeyWordsIsEnabled = true;
                ShowMsgMaterial($"Pre-processing complete! Elapsed time: {time} ms");
                CurrentExpanded = ExpandedValues.KeyWords;
            }

            PreprocessDataProgressVisibility = false;

            TestKnn();

        }

        private void TestKnn()
        {
            KnnClassifier knn = new KnnClassifier(_keyWords, new List<IFeatureService>()
            {
                /*new NGrammFeatureService(),
                new NiewiadomskiNGrammFeatureService(),
                //new BinaryFeatureService(),
                new KeywordFrequencyFeatureService(),
                //new Keyword20PercentFrequencyService(),
                new LevenshteinFeatureService(),*/
                new PercentOfKeyWordsService()
             }, new EuclideanDistance(), 10);
            List<string> temp = new List<string>();
            foreach (var VARIABLE in CategoryItems)
            {
                if (VARIABLE.IsSelected)
                {
                    temp.Add(VARIABLE.Name);
                }
            }
            knn.EnterColdStartArticles(ClassificationHelpers.GetNArticlesForColdStart(ref _trainingArticles, temp, 15));
            List<ClassificationModel> articles = new List<ClassificationModel>();
            foreach (var classificationModel in _trainingArticles)
            {
                articles.Add(knn.ClassifyArticle(classificationModel));
            }

            double lol = 0.0;
            foreach (var classificationModel in articles)
            {
                if (classificationModel.PredictedTag == classificationModel.Tag)
                {
                    lol++;
                }
            }

            lol /= articles.Count;
        }

        private void ExtractKeyWords()
        {
            var temp = KeyWordsMethodSelected.Substring(0, 1);

            switch (temp)
            {
                case "1":
                    _keyWords = KeyWordsExtractor.GetKeyWordsTF(_learningArticles, NumberOfKeyWordsTB, CategoryComboboxSelected, _stopList);
                    break;
                case "2":
                    _keyWords = KeyWordsExtractor.GetKeyWordsTFExtended(_learningArticles, NumberOfKeyWordsTB, CategoryComboboxSelected, _stopList, KeyWordsFilterWordsTB);
                    break;
                default:
                    throw new ArgumentException("You need to select key-words extraction method");

            }
        }

        private void LoadKeyWordsToGui()
        {
            KeyWordsItems = new ObservableCollection<KeyWordsVM>();
            foreach (var keyWord in _keyWords)
            {
                foreach (var VARIABLE in keyWord.Value)
                {
                    KeyWordsItems.Add(new KeyWordsVM()
                    {
                        Tag = keyWord.Key,
                        Word = VARIABLE
                    });
                }
            }
        }

        private void FilterArticles(List<ArticleModel> articles, List<SelectableVM> pickedItems, string category)
        {
            List<string> selected = new List<string>();
            foreach (var selectableViewModel in pickedItems)
            {
                if (selectableViewModel.IsSelected)
                {
                    selected.Add(selectableViewModel.Name);
                }
            }

            _filteredOutArticles =
                ArticleMetadataExtractor.GetArticlesFromCategoryAndMetadata(category, selected, articles);
        }

        private void SplitArticles(int percentage)
        {
            int allArticles = _filteredOutArticles.Count;
            int learningArticles = allArticles * percentage / 100;
            _learningArticles = _filteredOutArticles.Take(learningArticles).ToList();
            _trainingArticles = ClassificationHelpers.ConvertToClassificationModels(_filteredOutArticles.Skip(learningArticles).ToList(), CategoryComboboxSelected, _stopList);
        }

        #endregion

        #region File-handling methods

        private void LoadStopList()
        {
            _stopList = new List<string>();
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Text File(*txt)| *.txt",
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _stopList = File.ReadAllLines(dialog.FileName).ToList();
                    SelectStopListText = $"Loaded {_stopList.Count} words!";
                    NumOfStopListVisibility = true;
                    IsEnabledPreProcessBTN = true;
                }
                catch (Exception e)
                {
                    ShowErrorMaterial(e.Message);
                }
            }
        }


        private async void LoadArticlesFromCatalog()
        {
            string path = "";
            IsArticleLoadingVisible = true;
            using (CommonOpenFileDialog fileDialog = new CommonOpenFileDialog())
            {
                fileDialog.IsFolderPicker = true;
                fileDialog.RestoreDirectory = true;
                fileDialog.AllowNonFileSystemItems = true;
                if (fileDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    path = fileDialog.FileName;
                }
            }

            bool success = false;

            await Task.Run(() =>
            {
                try
                {
                    _articles = SGMLReader.ReadAllSGMLFromDirectory(path);
                    SelectArticleText = $"Loaded {_articles.Count} articles!";
                    NumOfArticlesVisibility = true;
                    LoadCategories(_articles);
                    success = true;
                    IsEnabledStopListBTN = true;
                }
                catch (Exception e)
                {
                    _errorMessage = e.Message;
                }

            });

            if (success)
            {
                ShowMsgMaterial("Articles loaded! Now please provide stop list.");
            }
            else
            {
                ShowErrors();
            }

            IsArticleLoadingVisible = false;

        }
        #endregion
        #region Window content loading methods

        private void LoadCategories(List<ArticleModel> articles)
        {
            CategoryCombobox = new ObservableCollection<string>(ArticleMetadataExtractor.GetCategories(articles));
        }

        private void LoadCategoryItems(string category, List<ArticleModel> articles)
        {
            CategoryItems = new ObservableCollection<SelectableVM>();
            var items = ArticleMetadataExtractor.GetCategoryItems(category, articles);
            foreach (var item in items)
            {
                CategoryItems.Add(new SelectableVM()
                {
                    Name = item,
                    Count = ArticleMetadataExtractor.GetNumberOfArticlesInCategoryForMetadata(category, item, articles)
                });
            }

            CategoryItems = new ObservableCollection<SelectableVM>(CategoryItems.OrderByDescending(t => t.Count).ToList());
        }

        private void LoadDefaultValues()
        {
            if (CategoryComboboxSelected == "places")
            {
                foreach (var selectableViewModel in CategoryItems)
                {
                    switch (selectableViewModel.Name)
                    {
                        case "usa":
                            selectableViewModel.IsSelected = true;
                            break;
                        case "west-germany":
                            selectableViewModel.IsSelected = true;
                            break;
                        case "france":
                            selectableViewModel.IsSelected = true;
                            break;
                        case "uk":
                            selectableViewModel.IsSelected = true;
                            break;
                        case "canada":
                            selectableViewModel.IsSelected = true;
                            break;
                        case "japan":
                            selectableViewModel.IsSelected = true;
                            break;
                    }
                }

            }
            else
            {
                ShowErrorMaterial("Places must be selected to load default values");
            }
        }
        #endregion
        #region MessageBox Section

        private void ShowMsgMaterial(string text)
        {
            var msg = new CustomMaterialMessageBox()
            {
                TxtMessage = { Text = text, TextAlignment = TextAlignment.Center, Foreground = _foreground },
                TxtTitle = { Text = "Message", Foreground = _foreground },
                MainContentControl = { Background = _background },
                TitleBackgroundPanel = { Background = Brushes.DarkViolet },
                BtnCancel = { Visibility = Visibility.Collapsed },
                BtnOk = { Background = Brushes.DarkViolet },
                BorderBrush = Brushes.DarkViolet,

            };
            msg.BtnOk.Focus();
            msg.Show();
        }
        private void ShowErrorMaterial(string text)
        {
            var msg = new CustomMaterialMessageBox()
            {
                TxtTitle = { Text = "ERROR" },
                TxtMessage = { Text = "Error has ocurred: " + text, Foreground = _foreground },
                TitleBackgroundPanel = { Background = (Brush)Brushes.Red },
                BorderBrush = (Brush)Brushes.Red,
                MainContentControl = { Background = _background },
                BtnCancel = { Visibility = Visibility.Collapsed },

            };
            msg.BtnOk.Focus();
            msg.Show();
        }

        private void ShowErrors()
        {
            if (!string.IsNullOrEmpty(_errorMessage))
            {
                ShowErrorMaterial(_errorMessage);
                _errorMessage = null;
            }
        }
        #endregion

    }
}
