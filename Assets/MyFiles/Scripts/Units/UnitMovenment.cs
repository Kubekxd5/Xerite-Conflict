using System;
using Mirror;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Unit))]
public class UnitMovenment : NetworkBehaviour
{
    private Camera _mainCam;
    [SerializeField] private Animator _unitAnimator = null;
    [SerializeField] private NavMeshAgent agent = null;

    private Unit unit;

    public LayerMask ground;

    [SyncVar]
    private bool _isRunning;

    private void Awake()
    {
        unit = GetComponent<Unit>();
    }

    #region Server

    [Command]
    private void CmdMove(Vector3 position)
    {
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
        {
            return;
        }

        agent.SetDestination(hit.position);
    }

    [Command]
    public void CmdSetRun(bool running)
    {
        _isRunning = running;
    }

    #endregion

    #region Client

    public override void OnStartAuthority()
    {
        _mainCam = Camera.main;
    }

    [ClientCallback]
    private void Update()
    {
        CmdSetRun(agent.velocity.magnitude > 0f);
        _unitAnimator.SetBool("isRunning", _isRunning);

        // Not our unit or not selected? Ignore input
        if (!isOwned || !unit.IsSelected) { return; }

        if (!Mouse.current.rightButton.wasPressedThisFrame) { return; }

        Ray ray = _mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, ground))
        {
            CmdMove(hit.point);
            agent.SetDestination(hit.point);
        }
    }

    #endregion
}