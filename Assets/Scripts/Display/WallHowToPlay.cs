using System;
using System.Collections;
using FoodIsekaiZ.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // The HOW TO PLAY board on the wall, opened by the Fortal web launcher's Tutorial button (through
    // ArenaRemoteLink) during the ready phase. The wall dims, the parchment board springs up, and the
    // pages turn by themselves every PageSeconds; the board drops away after the last page or when closed.
    // Each page shows a picture from the game in the board's round medallion, its title and a line
    // on what to do; the dots under it say which page it is. The kiosk is retired, so there are no buttons: everything is on the wall.
    // The board is built in code on its own canvas over the wall; its pictures load from
    // Resources/UI/HowToPlay (the board itself was drawn by Codex in the style of the Result panel).
    // Words follow GameLanguage (T on the keyboard or the launcher's TH/EN) and swap in place.
    // Runs on unscaled time. Console lines start with [WallHowToPlay].
    public sealed class WallHowToPlay : MonoBehaviour
    {
        // Over the wall's pictures and transitions.
        private const int SortingOrder = 200;
        private const string ArtFolder = "UI/HowToPlay/";

        // The words and picture for one page. Thai lines carry zero-width spaces (U+200B) where a
        // line may break, since Thai has no spaces between words. Badge is written over the picture
        // when it is not empty (the number on the ready card).
        private sealed class Page
        {
            public Page(string picture, string badge, string englishTitle, string thaiTitle, string englishBody, string thaiBody)
            {
                Picture = picture;
                Badge = badge;
                EnglishTitle = englishTitle;
                ThaiTitle = thaiTitle;
                EnglishBody = englishBody;
                ThaiBody = thaiBody;
            }

            public string Picture { get; }
            public string Badge { get; }
            public string EnglishTitle { get; }
            public string ThaiTitle { get; }
            public string EnglishBody { get; }
            public string ThaiBody { get; }
        }

        private static readonly Page[] Pages =
        {
            new Page("Step1Number", "1", "PICK YOUR NUMBER", "เลือก​หมายเลข​ของ​คุณ",
                "Stand on a number spot on the floor for 2 seconds. That number is you for the whole game.",
                "ยืน​บน​จุด​หมายเลข​บน​พื้น 2 วินาที หมายเลข​นั้น​คือ​ตัว​คุณ​ตลอด​เกม"),
            new Page("Step2Food", "", "GRAB THE FOOD", "หยิบ​อาหาร",
                "Walk onto a food station to pick up its dish. Your plate holds one of each dish.",
                "เดิน​ไป​ที่​จุด​อาหาร​เพื่อ​หยิบ​จาน​นั้น จาน​ของ​คุณ​ถือ​อาหาร​ได้​อย่าง​ละ 1 จาน"),
            new Page("Step3Customer", "", "SERVE THE CUSTOMERS", "เสิร์ฟ​ลูกค้า",
                "Bring each customer the dish on their order before they get angry and leave. Omakase eats anything!",
                "นำ​อาหาร​ที่​ลูกค้า​สั่ง​ไป​ส่ง​ก่อน​ลูกค้า​โกรธ​แล้ว​เดิน​ออก Omakase กิน​อะไร​ก็ได้!"),
            new Page("Step4Coins", "", "BANK THE COINS", "เก็บ​เหรียญ​เข้า​ธนาคาร",
                "Pick up the coins a happy customer leaves, then carry them to the Cashier.",
                "เก็บ​เหรียญ​ที่​ลูกค้า​จ่าย แล้ว​นำ​ไป​ฝาก​ที่​ธนาคาร​กิลด์"),
            new Page("Step5Perk", "", "BUY PERKS ON BREAKS", "ซื้อ​เพิร์ค​ตอน​พัก",
                "Between meals, stand on a perk card to buy it with team coins. Coins left at the end become bonus score.",
                "ระหว่าง​มื้อ ยืน​บน​การ์ด​เพิร์ค​เพื่อ​ซื้อ​ด้วย​เงิน​ทีม เหรียญ​ที่​เหลือ​ตอน​จบ​เกม​กลาย​เป็น​คะแนน​โบนัส"),
        };

        // Wall canvas units (the wall canvas is 1536 x 435). The board art is 3 : 1.
        private static readonly Vector2 BoardSize = new Vector2(1110f, 370f);
        // The round medallion in the board art, measured from its centre (units of BoardSize).
        private static readonly Vector2 MedallionCentre = new Vector2(-359f, -29f);
        private const float PictureSize = 210f;
        // The text column to the right of the medallion.
        private const float TextCentreX = 150f;
        private const float TextWidth = 680f;

        private static readonly Color DimColour = new Color(0.08f, 0.04f, 0.02f, 0.6f);
        private static readonly Color TitleColour = new Color(0.36f, 0.2f, 0.11f, 1f);
        private static readonly Color BodyColour = new Color(0.45f, 0.3f, 0.19f, 1f);
        private static readonly Color BadgeColour = new Color(0.36f, 0.2f, 0.11f, 1f);
        private static readonly Color DotOn = new Color(0.78f, 0.45f, 0.2f, 1f);
        private static readonly Color DotOff = new Color(0.88f, 0.72f, 0.47f, 0.7f);

        private const float PageSeconds = 8f;
        private const float OpenSeconds = 0.5f;
        private const float CloseSeconds = 0.22f;
        private const float PageOutSeconds = 0.18f;
        private const float PageInSeconds = 0.4f;
        private const float OpenRise = 110f;
        private const float BobHeight = 6f;
        private const float BobSpeed = 2.2f;

        private RectTransform wall;
        private TMP_FontAsset englishFont;
        private TMP_FontAsset thaiFont;
        private Sprite[] pictures;

        private CanvasGroup group;
        private RectTransform board;
        private CanvasGroup pageGroup;
        private Image picture;
        private TMP_Text badge;
        private TMP_Text title;
        private TMP_Text body;
        private Image[] dots;
        private Coroutine showing;
        private int current;
        private float pageTime;
        // From 0 (none) to 1 (full): how far the picture has popped in; it bobs once it is full.
        private float pop;
        private bool open;
        // True while the board springs up, before its first page has come in.
        private bool poppingUp;
        // Set once the launcher turns a page itself: the pages then stop turning by themselves.
        private bool heldByLauncher;

        // Raised when the board finishes dropping away, whether closed or run out of pages.
        public event Action Closed;

        // True from Open until the board starts to drop away.
        public bool IsOpen => open;

        // The page on show, counted from 1.
        public int PageNumber => current + 1;

        public int PageCount => Pages.Length;

        // Turns to this page (counted from 1) from the launcher's BACK / NEXT, and from then on the
        // pages wait for it instead of turning by themselves. Ignored while closed or out of range.
        public void GoToPage(int number)
        {
            int index = number - 1;
            if (!open || index < 0 || index >= Pages.Length) return;

            heldByLauncher = true;
            if (index == current) return;
            if (poppingUp)
            {
                // The page comes in as the board finishes springing up.
                current = index;
                ShowPage(current);
                return;
            }

            Play(TurnTo(index));
        }

        // Builds the board (hidden) on the wall canvas. The fonts may be null; TMP's default is used then.
        public void Initialise(RectTransform wallCanvas, TMP_FontAsset english, TMP_FontAsset thai)
        {
            wall = wallCanvas;
            englishFont = english;
            thaiFont = thai;
            LoadPictures();
            Build();
            GameLanguage.Changed += OnLanguageChanged;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            GameLanguage.Changed -= OnLanguageChanged;
        }

        public void Open()
        {
            if (open || board == null) return;
            open = true;
            gameObject.SetActive(true);
            current = 0;
            heldByLauncher = false;
            ShowPage(current);
            Play(PopUp());
            Debug.Log("[WallHowToPlay] Opened", this);
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            poppingUp = false;
            Play(DropAway());
            Debug.Log("[WallHowToPlay] Closed", this);
        }

        private void OnLanguageChanged(GameLanguageId language)
        {
            if (open) ShowPage(current);
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            RectTransform pictureRect = picture.rectTransform;
            float bob = Mathf.Clamp01(pop);
            pictureRect.anchoredPosition = MedallionCentre + new Vector2(0f, Mathf.Sin(time * BobSpeed) * BobHeight * bob);
            pictureRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * BobSpeed * 0.5f) * 3f * bob);

            if (!open || showing != null || heldByLauncher) return;

            pageTime += Time.unscaledDeltaTime;
            if (pageTime < PageSeconds) return;
            if (current >= Pages.Length - 1) Close();
            else Play(TurnTo(current + 1));
        }

        private void LoadPictures()
        {
            pictures = new Sprite[Pages.Length];
            for (int i = 0; i < Pages.Length; i++)
            {
                pictures[i] = Resources.Load<Sprite>(ArtFolder + Pages[i].Picture);
                if (pictures[i] == null) Debug.LogWarning($"[WallHowToPlay] Missing picture Resources/{ArtFolder}{Pages[i].Picture}", this);
            }
        }

        private void ShowPage(int index)
        {
            Page page = Pages[index];
            bool thai = GameLanguage.IsThai;
            TMP_FontAsset font = thai && thaiFont != null ? thaiFont : englishFont;
            foreach (TMP_Text text in new[] { title, body })
            {
                if (font != null && text.font != font) text.font = font;
            }

            picture.sprite = pictures[index];
            picture.enabled = pictures[index] != null;
            badge.text = page.Badge;
            title.text = thai ? page.ThaiTitle : page.EnglishTitle;
            body.text = thai ? page.ThaiBody : page.EnglishBody;
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i].color = i == index ? DotOn : DotOff;
                dots[i].rectTransform.localScale = Vector3.one * (i == index ? 1.6f : 1f);
            }

            pageTime = 0f;
        }

        private void Play(IEnumerator routine)
        {
            if (showing != null) StopCoroutine(showing);
            showing = StartCoroutine(Run(routine));
        }

        private IEnumerator Run(IEnumerator routine)
        {
            yield return routine;
            showing = null;
        }

        private IEnumerator PopUp()
        {
            poppingUp = true;
            PlacePicture(0f);
            pageGroup.alpha = 0f;
            for (float t = 0f; t < OpenSeconds; t += Time.unscaledDeltaTime)
            {
                float u = t / OpenSeconds;
                group.alpha = Mathf.Clamp01(u * 2.5f);
                float spring = EaseOutBack(u);
                PlaceBoard(Mathf.LerpUnclamped(0.7f, 1f, spring), 1f - spring, 3f * (1f - u) * Mathf.Sin(u * Mathf.PI * 3f));
                yield return null;
            }

            group.alpha = 1f;
            PlaceBoard(1f, 0f, 0f);
            poppingUp = false;
            yield return PageIn();
        }

        private IEnumerator DropAway()
        {
            float from = group.alpha;
            for (float t = 0f; t < CloseSeconds; t += Time.unscaledDeltaTime)
            {
                float u = t / CloseSeconds;
                group.alpha = from * (1f - u);
                PlaceBoard(Mathf.Lerp(1f, 0.85f, u * u), u * u * 0.5f, 0f);
                yield return null;
            }

            group.alpha = 0f;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        // The page fades out with its picture shrinking, then the next one comes in.
        private IEnumerator TurnTo(int page)
        {
            float from = pageGroup.alpha;
            for (float t = 0f; t < PageOutSeconds; t += Time.unscaledDeltaTime)
            {
                float u = t / PageOutSeconds;
                pageGroup.alpha = from * (1f - u);
                PlacePicture(1f - u);
                yield return null;
            }

            current = page;
            ShowPage(current);
            yield return PageIn();
        }

        // The words fade up while the picture pops in with a little overshoot.
        private IEnumerator PageIn()
        {
            float total = PageInSeconds * 1.4f;
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                pageGroup.alpha = Mathf.Clamp01(t / PageInSeconds);
                PlacePicture(EaseOutBack(Mathf.Clamp01((t - PageInSeconds * 0.2f) / PageInSeconds)));
                yield return null;
            }

            pageGroup.alpha = 1f;
            PlacePicture(1f);
            pageTime = 0f;
        }

        private void PlaceBoard(float scale, float lowered, float tilt)
        {
            board.localScale = Vector3.one * scale;
            board.anchoredPosition = new Vector2(0f, -lowered * OpenRise);
            board.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        private void PlacePicture(float size)
        {
            pop = size;
            picture.rectTransform.localScale = Vector3.one * Mathf.Max(0f, size);
        }

        // Dim layer over the whole wall, the parchment board in the middle, and the page on it.
        private void Build()
        {
            int layer = wall.gameObject.layer;
            gameObject.layer = layer;
            var root = (RectTransform)transform;
            root.SetParent(wall, false);
            Stretch(root);
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            Image dim = Box("Dim", root, layer, DimColour);
            Stretch(dim.rectTransform);

            Image boardImage = Box("Board", root, layer, Color.white);
            boardImage.sprite = Resources.Load<Sprite>(ArtFolder + "HowToPlayBoard");
            if (boardImage.sprite == null) Debug.LogWarning($"[WallHowToPlay] Missing Resources/{ArtFolder}HowToPlayBoard", this);
            board = boardImage.rectTransform;
            board.sizeDelta = BoardSize;

            var pageObject = new GameObject("Page", typeof(RectTransform), typeof(CanvasGroup));
            pageObject.layer = layer;
            var page = (RectTransform)pageObject.transform;
            page.SetParent(board, false);
            Stretch(page);
            pageGroup = pageObject.GetComponent<CanvasGroup>();

            picture = Box("Picture", page, layer, Color.white);
            picture.preserveAspect = true;
            picture.rectTransform.sizeDelta = new Vector2(PictureSize, PictureSize);
            picture.rectTransform.anchoredPosition = MedallionCentre;
            badge = Label("Badge", picture.rectTransform, layer, 110f, BadgeColour, Vector2.zero, new Vector2(PictureSize, PictureSize));
            badge.fontStyle = FontStyles.Bold;

            title = Label("Title", page, layer, 58f, TitleColour, new Vector2(TextCentreX, 55f), new Vector2(TextWidth, 70f));
            title.fontStyle = FontStyles.Bold;
            body = Label("Body", page, layer, 32f, BodyColour, new Vector2(TextCentreX, -42f), new Vector2(TextWidth - 40f, 110f));

            var row = new GameObject("Dots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.layer = layer;
            var rowRect = (RectTransform)row.transform;
            rowRect.SetParent(board, false);
            rowRect.anchoredPosition = new Vector2(TextCentreX, -133f);
            rowRect.sizeDelta = new Vector2(300f, 20f);
            var across = row.GetComponent<HorizontalLayoutGroup>();
            across.spacing = 24f;
            across.childAlignment = TextAnchor.MiddleCenter;
            across.childControlWidth = false;
            across.childControlHeight = false;
            across.childForceExpandWidth = false;
            dots = new Image[Pages.Length];
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i] = Box("Dot", rowRect, layer, DotOff);
                dots[i].rectTransform.sizeDelta = new Vector2(11f, 11f);
                dots[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
        }

        private static Image Box(string name, RectTransform parent, int layer, Color colour)
        {
            var box = new GameObject(name, typeof(RectTransform), typeof(Image));
            box.layer = layer;
            box.transform.SetParent(parent, false);
            var image = box.GetComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text Label(string name, RectTransform parent, int layer, float size, Color colour, Vector2 position, Vector2 area)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            label.layer = layer;
            var rect = (RectTransform)label.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = area;
            var text = label.GetComponent<TextMeshProUGUI>();
            if (englishFont != null) text.font = englishFont;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * 0.5f;
            text.fontSizeMax = size;
            text.color = colour;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Runs a little past 1 before settling on it.
        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float back = t - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }
    }
}
