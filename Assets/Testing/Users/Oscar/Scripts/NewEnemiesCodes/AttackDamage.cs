/*using UnityEngine;

public class AttackDamage : MonoBehaviour {
    [SerializeField] private LifeManager m_lifeManager;
    [SerializeField] private ScreamerData_SO m_screamerData;
    [SerializeField] private int m_damageToDo;


    private void OnEnable()
    {
        if (m_lifeManager == null)
        {
            m_lifeManager = LifeManager.instance;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            LifeManager.instance.TakeDamage(m_damageToDo);
            ScreamersManager.instance?.SetScreamerData(m_screamerData);
        }
    }
}*/