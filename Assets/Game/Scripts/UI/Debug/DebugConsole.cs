using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI.DebugTools
{
    /// <summary>
    /// 局内调试控制台：按 ` （反引号 / · 键）开关，输入命令直接改游戏状态。
    /// 面板本身是预制体（Resources/UI/Debug/DebugConsolePanel），这里只负责输入与渲染。
    /// </summary>
    public sealed class DebugConsole : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.BackQuote;
        private const int MaxLines = 60;

        public static DebugConsole Instance { get; private set; }

        private GameObject m_Panel;
        private TMP_Text m_Output;
        private TMP_Text m_Input;

        private readonly List<string> m_Log = new List<string>();
        private readonly List<string> m_History = new List<string>();
        private readonly StringBuilder m_Builder = new StringBuilder();
        private int m_HistoryCursor = -1;
        private string m_Buffer = string.Empty;
        private bool m_Visible;

        /// <summary>进游戏后自动挂一个，不需要改场景。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
            {
                return;
            }

            var go = new GameObject("[DebugConsole]");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugConsole>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildCanvas();
            Write("调试控制台就绪：` 开关，help 查看命令");
            SetVisible(false);
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject prefab = Resources.Load<GameObject>("UI/Debug/DebugConsolePanel");
            if (prefab == null)
            {
                Debug.LogError("[调试台] 缺少预制体 Resources/UI/Debug/DebugConsolePanel");
                return;
            }

            m_Panel = Instantiate(prefab, canvasGo.transform, false);
            m_Panel.name = "DebugConsolePanel";

            m_Output = FindText("txtOutput");
            m_Input = FindText("txtInput");
            if (m_Output == null || m_Input == null)
            {
                Debug.LogError("[调试台] 预制体里缺少 txtOutput / txtInput");
            }
        }

        private TMP_Text FindText(string name)
        {
            if (m_Panel == null)
            {
                return null;
            }

            foreach (TMP_Text t in m_Panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                SetVisible(!m_Visible);
                return;
            }

            if (!m_Visible)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetVisible(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                BrowseHistory(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                BrowseHistory(-1);
                return;
            }

            string typed = Input.inputString;
            if (string.IsNullOrEmpty(typed))
            {
                return;
            }

            foreach (char c in typed)
            {
                if (c == '\b')
                {
                    if (m_Buffer.Length > 0)
                    {
                        m_Buffer = m_Buffer.Substring(0, m_Buffer.Length - 1);
                    }
                }
                else if (c == '\n' || c == '\r')
                {
                    Submit();
                }
                else if (c >= 32 && c != '`')
                {
                    m_Buffer += c;
                }
            }

            RefreshInput();
        }

        private void BrowseHistory(int direction)
        {
            if (m_History.Count == 0)
            {
                return;
            }

            m_HistoryCursor = Mathf.Clamp(m_HistoryCursor + direction, -1, m_History.Count - 1);
            m_Buffer = m_HistoryCursor < 0 ? string.Empty : m_History[m_HistoryCursor];
            RefreshInput();
        }

        private void Submit()
        {
            string line = m_Buffer.Trim();
            m_Buffer = string.Empty;
            RefreshInput();

            Write("> " + line);
            if (line.Length == 0)
            {
                return;
            }

            if (m_History.Count == 0 || m_History[m_History.Count - 1] != line)
            {
                m_History.Add(line);
            }

            m_HistoryCursor = -1;
            DebugCommands.Execute(line, this);
        }

        public void SetVisible(bool visible)
        {
            m_Visible = visible;
            if (m_Panel != null)
            {
                m_Panel.SetActive(visible);
            }

            if (visible)
            {
                m_Buffer = string.Empty;
                RefreshInput();
            }
        }

        public void Write(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            m_Log.Add(message);
            while (m_Log.Count > MaxLines)
            {
                m_Log.RemoveAt(0);
            }

            m_Builder.Length = 0;
            for (int i = 0; i < m_Log.Count; i++)
            {
                m_Builder.AppendLine(m_Log[i]);
            }

            if (m_Output != null)
            {
                m_Output.text = m_Builder.ToString();
            }
        }

        private void RefreshInput()
        {
            if (m_Input != null)
            {
                m_Input.text = "> " + m_Buffer + "_";
            }
        }
    }
}
