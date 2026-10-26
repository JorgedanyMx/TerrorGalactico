using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class Enemy_Manager : MonoBehaviour
{
    [SerializeField] private GameObject _enemyPreFab;
    [SerializeField] private GameObject _cardPrefab;
    [SerializeField] private Transform[] _enemySpawners;
    [SerializeField] private float _enemySpawnTime;


    private IEnumerator _corutine;

    private void Awake()
    {
        _corutine = SpawnEnemy();
        StartCoroutine(SpawnEnemy());
    }

    IEnumerator SpawnEnemy()
    {
        yield return new WaitForSeconds(_enemySpawnTime);
        int spawner = Random.Range(0, _enemySpawners.Length);

        GameObject Enemy = Instantiate(_enemyPreFab, _enemySpawners[spawner].transform.position, _enemySpawners[spawner].rotation);
        StartCoroutine(SpawnEnemy());
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("trigger activado");
        if (other.transform.tag.Equals("Ofrenda"))
        {
            print("ofrenda recivida");
            Destroy(this);
        }

        if (other.transform.tag.Equals("bullet"))
        {
            print("fantasma muerto");
            Instantiate(_cardPrefab, gameObject.transform.position, gameObject.transform.rotation);
            Destroy(this);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("collider activado");
    }
}
