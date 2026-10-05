using UnityEngine;
using Photon.Pun;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class StartManager:MonoBehaviourPunCallbacks
{
    [SerializeField]
    Text loading;
    [SerializeField]
    Animator animator;

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
}
