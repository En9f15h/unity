using UnityEngine;
using Photon.Pun;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using Photon.Realtime;
using System.Collections.Generic;

public class StartManager:MonoBehaviourPunCallbacks
{
    [SerializeField]
    Text loading;
    [SerializeField]
    Animator animator;

    private int regionFallbackIndex=0;
    private List<string> regionFallbackList=new List<string> {  "usw", "eu", "uae", "za" };
    private void Start()
    {
        AudioManager.Instance.PlayStartSceneBGM();
    }

    public void StartClick()
    {
        AudioManager.Instance.PlayStartSceneButton();
        PhotonNetwork.ConnectUsingSettings();
        animator.SetTrigger("Start");
        loading.text = "Loading...";
    }
    public override void OnConnectedToMaster()
    {

        StartCoroutine(WaitStartAnimationThenLoadScene());
    }

    private IEnumerator WaitStartAnimationThenLoadScene()
    {
        yield return null;

        
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        yield return new WaitForSeconds(stateInfo.length);

        SceneTransitionManager.RequestSceneTransition("LobbyScene");
    }
    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
        if (regionFallbackIndex >= regionFallbackList.Count - 1) return;

        PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = regionFallbackList[regionFallbackIndex];
        PhotonNetwork.ConnectUsingSettings();
        regionFallbackIndex++;
    }
}
