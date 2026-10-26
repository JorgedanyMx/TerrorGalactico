using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class Enemy_Manager : MonoBehaviour
{
    [SerializeField] private GameObject _enemyPreFab;
    [SerializeField] private Transform[] _enemySpawners;
    [SerializeField] private float _enemySpawnTime;
    public GameEvent SpawnOfrenda;
    public int maxEnemies = 15;
    private int currentSpawns = 0;


    private IEnumerator _corutine;

    private void Awake()
    {
        _corutine = SpawnEnemy();
        StartCoroutine(SpawnEnemy());
    }

    IEnumerator SpawnEnemy()
    {
        if (currentSpawns >= maxEnemies)
        {
            StopAllCoroutines();
        }
        yield return new WaitForSeconds(_enemySpawnTime);
        int spawner = Random.Range(0, _enemySpawners.Length);

        GameObject Enemy = Instantiate(_enemyPreFab, _enemySpawners[spawner].transform.position, _enemySpawners[spawner].rotation);
        currentSpawns += 1;
        SpawnOfrenda.Raise();

        StartCoroutine(SpawnEnemy());
    }
}
