using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using System.Linq;

public class BonusController : MonoBehaviour
{
  [SerializeField]
  private Button Spin_Button;
  [SerializeField]
  private RectTransform Wheel_Transform;
  [SerializeField]
  private BoxCollider2D[] point_colliders;
  [SerializeField]
  private TMP_Text[] Bonus_Text;
  [SerializeField]
  private GameObject Bonus_Object;
  [SerializeField]
  private SlotBehaviour slotManager;
  [SerializeField]
  private AudioController _audioManager;
  [SerializeField]
  private GameObject PopupPanel;
  [SerializeField]
  private Transform Win_Transform;
  [SerializeField]
  private Transform Loose_Transform;
  [SerializeField]
  private SocketIOManager m_SocketManager;

  internal bool isCollision = false;

  private Tween wheelRoutine;

  private float elasticIntensity = 5f;

  private int stopIndex = 0;


  private void Start()
  {
    if (Spin_Button) Spin_Button.onClick.RemoveAllListeners();
    if (Spin_Button) Spin_Button.onClick.AddListener(Spinbutton);
  }

  internal void StartBonus(int stop)
  {
    ResetColliders();
    Spin_Button.gameObject.SetActive(false);
    if (PopupPanel) PopupPanel.SetActive(false);
    if (Win_Transform) Win_Transform.gameObject.SetActive(false);
    if (Loose_Transform) Loose_Transform.gameObject.SetActive(false);
    PopulateWheel(m_SocketManager.initialData.spinBonus.Select(i => i.ToString()).ToList());
    if (_audioManager) _audioManager.SwitchBGSound(true);

    stopIndex = stop;
    if (Bonus_Object) Bonus_Object.SetActive(true);
    DOVirtual.DelayedCall(1f, () =>
    {
      Spinbutton();
    });
  }

  private void Spinbutton()
  {
    isCollision = false;
    if (Spin_Button) Spin_Button.interactable = false;
    RotateWheel();
    DOVirtual.DelayedCall(1f, () =>
    {
      TurnCollider(stopIndex);
    });
  }

  internal void PopulateWheel(List<string> bonusdata)
  {
    for (int i = 0; i < bonusdata.Count; i++)
    {
      if (bonusdata[i] == "-1" || bonusdata[i] == "0")
      {
        if (Bonus_Text[i]) Bonus_Text[i].text = "NO \nBONUS";
      }
      else
      {
        if (Bonus_Text[i]) Bonus_Text[i].text = (double.Parse(bonusdata[i]) * m_SocketManager.initialData.bets[slotManager.BetCounter]).ToString();
      }
    }
  }

  private void RotateWheel()
  {
    if (Wheel_Transform) Wheel_Transform.localEulerAngles = new Vector3(0, 0, 359);
    if (Wheel_Transform) wheelRoutine = Wheel_Transform.DORotate(new Vector3(0, 0, 0), 1, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1);
    _audioManager.PlayBonusAudio("cycleSpin");
  }

  private void ResetColliders()
  {
    foreach (BoxCollider2D col in point_colliders)
    {
      col.enabled = false;
    }
  }

  private void TurnCollider(int point)
  {
    if (point_colliders[point]) point_colliders[point].enabled = true;
  }

  internal void StopWheel()
  {
    if (wheelRoutine != null)
    {
      wheelRoutine.Pause(); // Pause the rotation

      // Apply an elastic effect to the paused rotation
      Wheel_Transform.DORotate(Wheel_Transform.eulerAngles + Vector3.forward * Random.Range(-elasticIntensity, elasticIntensity), 1f)
          .SetEase(Ease.OutElastic);
    }
    if (Bonus_Text[stopIndex].text.Equals("NO \nBONUS"))
    {
      if (Loose_Transform) Loose_Transform.gameObject.SetActive(true);
      if (Loose_Transform) Loose_Transform.localScale = Vector3.zero;
      if (PopupPanel) PopupPanel.SetActive(true);
      if (Loose_Transform) Loose_Transform.DOScale(Vector3.one, 1f);
      PlayWinLooseSound(false);
    }
    else
    {
      if (Win_Transform) Win_Transform.gameObject.SetActive(true);
      if (Win_Transform) Win_Transform.localScale = Vector3.zero;
      if (PopupPanel) PopupPanel.SetActive(true);
      if (Win_Transform) Win_Transform.DOScale(Vector3.one, 1f);
      PlayWinLooseSound(true);
    }
    DOVirtual.DelayedCall(3f, () =>
    {
      ResetColliders();
      if (_audioManager) _audioManager.SwitchBGSound(false);
      if (Bonus_Object) Bonus_Object.SetActive(false);
      slotManager.CheckWinPopups();
    });
  }

  internal void PlayWinLooseSound(bool isWin)
  {
    if (isWin)
    {
      _audioManager.PlayBonusAudio("win");
    }
    else
    {
      _audioManager.PlayBonusAudio("lose");
    }
  }
}
