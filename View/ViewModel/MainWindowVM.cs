﻿using _20NewsGroupsParser;
using BespokeFusion;
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
using System.Globalization;
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

        private bool _canRun = true;

        #endregion
        public ICommand SelectCatalogButton { get; }
        public ICommand SelectStopListButton { get; }
        public ICommand SelectDefaultButton { get; }
        public ICommand PreProcessButton { get; }
        public ICommand CategorizeButton { get; set; }
        public ICommand SaveButton { get; set; }


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
                if (value != null)
                {
                    LoadCategoryItems(value, _articles);
                }

            }
        }
        public ObservableCollection<SelectableVM> CategoryItems { get; set; }
        public ObservableCollection<KeyWordsVM> KeyWordsItems { get; set; }

        public int AmountLearningDataSlider { get; set; } = 60;
        public bool IsEnabledPreProcessBTN { get; set; }
        public bool PreprocessDataProgressVisibility { get; set; }
        public int NumberOfKeyWordsTB { get; set; } = 15;
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
        public int KeyWordsFilterWordsTB { get; set; } = 2;

        #region Switches

        public bool BinaryToggleChecked { get; set; }
        public bool TwentyFreqToggleChecked { get; set; }
        public bool FreqToggleChecked { get; set; }
        public bool LevenshteinToggleChecked { get; set; }
        public bool NiewiadomskiNGrammToggleChecked { get; set; }
        public bool PercentToggleChecked { get; set; } = true;
        public bool NGrammToggleChecked { get; set; }
        public int NGrammParamTB { get; set; } = 3;

        #endregion

        public int KParamTB { get; set; } = 10;
        public int ColdStartTB { get; set; } = 15;

        public ObservableCollection<string> MetricsCombobox { get; set; } = new ObservableCollection<string>()
        {
            "Euclidean Distance",
            "City Metric",
            "Chebyshev Distance"
        };
        public string MetricsSelected { get; set; }

        public ObservableCollection<CategorizedItem> CategorizedItems { get; set; }

        public double CategorizeButtonProgress { get; set; }

        public string CategorizeButtonText { get; set; } = "Categorize Articles";
        public SaveDataModel SaveData { get; private set; }

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
            CategorizeButton = new RelayCommand(Categorize);
            SaveButton = new RelayCommand(SaveDataToFile);
        }

        #region Theme Solvers

        private void ReadWindowsSetting()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
            {
                object registryValueObject = key?.GetValue(RegistryValueName);
                if (registryValueObject == null)
                {
                    _isDarkTheme = false;
                }

                int registryValue;
                if (registryValueObject == null)
                {
                    registryValue = 0;
                }
                else
                {
                    registryValue = (int)registryValueObject;
                }

                _isDarkTheme = registryValue <= 0;
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

        private void SaveDataToFile()
        {
            SaveFileDialog sfd = new SaveFileDialog()
            {
                AddExtension = true,
                DefaultExt = "txt",
                Filter = "Text files (*.txt)|*.txt"
            };
            var fileName = "KSR_" + DateTime.Now.ToShortTimeString();
            sfd.FileName = fileName.Replace(':', '_');
            var result = sfd.ShowDialog();
            if (result == true)
            {
                TextWriter tw = new StreamWriter(sfd.FileName);
                tw.WriteLine("Category: " + SaveData.Category);
                tw.WriteLine("Tags:");
                foreach (var tag in SaveData.Tags)
                {
                    tw.WriteLine(@" " + tag);
                }
                tw.WriteLine("Percent of learning data: " + SaveData.LearningDataPercent + "%");
                tw.WriteLine("Key Words Extraction Method: " + SaveData.KeyWordsExtractionMethod);
                if (!SaveData.KeyWordsExtendedPercent.Equals(0))
                {
                    tw.WriteLine("Extended Key Words filter percentage: " + SaveData.KeyWordsExtendedPercent + "%");
                }


                tw.WriteLine("Key Words amount per tag: " + SaveData.KeyWordsCount);
                tw.WriteLine("Articles pre-processing time: " + SaveData.KeyWordsSearchTime + "ms");
                tw.WriteLine("------CATEGORIZATION------");
                tw.WriteLine("KNN K parameter: " + SaveData.Kparam);
                tw.WriteLine("Features:");
                foreach (var feature in SaveData.UsedFeatures)
                {
                    tw.WriteLine(@" " + feature);
                }
                if (SaveData.NGrammNParam != 0)
                {
                    tw.WriteLine(@"     N Gramm parameter: " + SaveData.NGrammNParam);
                }
                tw.WriteLine("Number of cold start articles for each category: " + SaveData.ColdStartCount);
                tw.WriteLine("Metric: " + SaveData.Metric);
                tw.WriteLine("Accuracy: " + SaveData.AccuracyPercent + "%");
                tw.WriteLine("Categorization time: " + SaveData.CategorizationTime + "ms");
                tw.Close();
                Process.Start(sfd.FileName);
            }
            
        }

        #region Categorization Methods

        private void AbortCategorization()
        {
            _canRun = false;
        }

        private async void Categorize()
        {

            if (string.IsNullOrEmpty(MetricsSelected))
            {
                ShowErrorMaterial("Choose Metric before categorizing");
                return;
            }
            SaveData.UsedFeatures = new List<string>();
            var featureServices = GetFeatureServices();
            if (featureServices.Count == 0)
            {
                ShowErrorMaterial("Choose at least 1 Feature before categorizing");
                return;
            }

            CategorizeButton = new RelayCommand(AbortCategorization);
            CategorizeButtonText = "Click To Abort";

            var articlesToProcess = new List<ClassificationModel>(_trainingArticles);
            var Metric = GetMetric();
            GenerateListOfCategorized();
            KnnClassifier knn = new KnnClassifier(_keyWords, featureServices, Metric, KParamTB);
            knn.EnterColdStartArticles(ClassificationHelpers.GetNArticlesForColdStart(ref articlesToProcess, GetAllTags(), ColdStartTB));
            CurrentExpanded = ExpandedValues.None;
            CurrentExpanded = ExpandedValues.Categorized;
            bool success = false;
            double categorized = 0;
            double time = 0;
            Stopwatch timer = new Stopwatch();

            SaveData.ColdStartCount = ColdStartTB;
            SaveData.Kparam = KParamTB;



            await Task.Run(() =>
            {
                try
                {
                    timer.Start();

                    foreach (var classificationModel in articlesToProcess)
                    {
                        if (_canRun)
                        {
                            AddArticleToCategorized(knn.ClassifyArticle(classificationModel));
                            categorized++;
                            CategorizeButtonProgress = categorized / articlesToProcess.Count * 100;
                        }
                        else
                        {
                            break;
                        }

                    }
                    timer.Stop();
                    time = timer.ElapsedMilliseconds;
                    success = true;
                }
                catch (Exception e)
                {
                    timer.Stop();
                    _errorMessage = e.Message;
                }
            });
            if (!success)
            {
                ShowErrors();
            }
            else
            {
                if (!_canRun)
                {
                    ShowErrorMaterial($"Classification INCOMPLETE. Process Aborted by user. Accuracy: {GetAccuracy() * 100}%. Time elapsed: {time}ms");

                }
                else
                {
                    ShowMsgMaterial($"Classification complete. Accuracy: {GetAccuracy() * 100}%. Time elapsed: {time}ms");
                }
            }

            CategorizeButtonText = "Categorize Articles";
            CategorizeButton = new RelayCommand(Categorize);
            _canRun = true;
            CategorizeButtonProgress = 0;

            SaveData.AccuracyPercent = GetAccuracy() * 100;
            SaveData.CategorizationTime = time;
            SaveData.Metric = MetricsSelected;


        }

        private List<IFeatureService> GetFeatureServices()
        {
            List<IFeatureService> temp = new List<IFeatureService>();
            if (BinaryToggleChecked)
            {
                var service = new BinaryFeatureService();
                temp.Add(service);
                SaveData.UsedFeatures.Add(service.ToString());
            }

            if (TwentyFreqToggleChecked)
            {
                var service = new Keyword20PercentFrequencyService();
                SaveData.UsedFeatures.Add(service.ToString());
                temp.Add(service);
            }

            if (FreqToggleChecked)
            {
                var service = new KeywordFrequencyFeatureService();
                SaveData.UsedFeatures.Add(service.ToString());
                temp.Add(service);
            }

            if (LevenshteinToggleChecked)
            {
                var service = new LevenshteinFeatureService();
                SaveData.UsedFeatures.Add(service.ToString());
                temp.Add(service);
            }

            if (NiewiadomskiNGrammToggleChecked)
            {
                var service = new NiewiadomskiNGrammFeatureService();
                SaveData.UsedFeatures.Add(service.ToString());
                temp.Add(service);
            }

            if (PercentToggleChecked)
            {
                var service = new PercentOfKeyWordsService();
                SaveData.UsedFeatures.Add(service.ToString());
                temp.Add(service);
            }

            if (NGrammToggleChecked)
            {
                var service = new NGrammFeatureService(NGrammParamTB);
                SaveData.UsedFeatures.Add(service.ToString());
                SaveData.NGrammNParam = NGrammParamTB;
                temp.Add(service);
            }

            return temp;
        }

        private IDistance GetMetric()
        {
            switch (MetricsSelected)
            {
                case "Euclidean Distance":
                    return new EuclideanDistance();
                case "City Metric":
                    return new CityMetric();
                case "Chebyshev Distance":
                    return new ChebyshevDistance();
            }

            return null;
        }

        private List<string> GetAllTags()
        {
            List<string> temp = new List<string>();
            foreach (var item in CategoryItems)
            {
                if (item.IsSelected)
                {
                    temp.Add(item.Name);
                }
            }

            return temp;
        }

        private void GenerateListOfCategorized()
        {
            CategorizedItems = new ObservableCollection<CategorizedItem>();
            foreach (var allCategory in GetAllTags())
            {
                CategorizedItems.Add(new CategorizedItem()
                {
                    Tag = allCategory
                });
            }
        }

        private void AddArticleToCategorized(ClassificationModel article)
        {
            var predicted = article.PredictedTag;
            foreach (var item in CategorizedItems)
            {
                if (item.Tag == article.Tag)
                {
                    item.All++;
                    if (article.Tag == predicted)
                    {
                        item.TruePositive++;
                    }
                    else
                    {
                        item.FalseNegative++;
                        foreach (var categorizedItem in CategorizedItems)
                        {
                            if (predicted == categorizedItem.Tag)
                            {
                                categorizedItem.FalsePositive++;
                                break;
                            }
                        }
                    }

                    return;
                }
            }
        }

        private double GetAccuracy()
        {
            double temp = 0;
            foreach (var categorizedItem in CategorizedItems)
            {
                temp += categorizedItem.TruePositive;
            }

            return Math.Round(temp / _trainingArticles.Count, 3);
        }
        #endregion
        #region Pre-process Data Methods  
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
            SaveData = new SaveDataModel();
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


            SaveData.Category = CategoryComboboxSelected;
            SaveData.Tags = GetAllTags();
            SaveData.KeyWordsSearchTime = time;
            SaveData.LearningDataPercent = AmountLearningDataSlider;
            SaveData.KeyWordsCount = NumberOfKeyWordsTB;
            SaveData.KeyWordsExtractionMethod = KeyWordsMethodSelected;


            PreprocessDataProgressVisibility = false;
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
                    SaveData.KeyWordsExtendedPercent = KeyWordsFilterWordsTB;
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
                    var files = Directory.GetFiles(path, "*.sgm");
                    _articles = files.Length != 0 ? SGMLReader.ReadAllSGMLFromDirectory(files) : NewsReader.ReadAllNewsFromDirectory(path);
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
