-- MT-5B-R1 reconstructed candidate; NOT the lost certified SQL.
-- Review only. Never run against the MT4C baseline on port 3306.
-- MySQL 5.7.24, quiescent writers, verified backup and reviewed driver required.
-- Two zero rules; invoke only after additive 004.
-- Body checks here supplement, never replace full normalized-body comparison.
SELECT 'delete_contract_presence' AS rule,
 IF(EXISTS(SELECT 1 FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()
  AND TRIGGER_NAME='mt5b_membership_delete_contract' AND EVENT_OBJECT_TABLE='institution_membership'
  AND ACTION_TIMING='BEFORE' AND EVENT_MANIPULATION='DELETE'
  AND LOCATE('1451',ACTION_STATEMENT)>0 AND LOCATE('23000',ACTION_STATEMENT)>0),0,1) AS violations
UNION ALL
SELECT 'delete_contract_order',
 IF(EXISTS(SELECT 1 FROM information_schema.TRIGGERS contract
  JOIN information_schema.TRIGGERS core ON core.TRIGGER_SCHEMA=contract.TRIGGER_SCHEMA
   AND core.EVENT_OBJECT_TABLE=contract.EVENT_OBJECT_TABLE
   AND core.ACTION_TIMING=contract.ACTION_TIMING AND core.EVENT_MANIPULATION=contract.EVENT_MANIPULATION
  WHERE contract.TRIGGER_SCHEMA=DATABASE() AND contract.EVENT_OBJECT_TABLE='institution_membership'
   AND contract.ACTION_TIMING='BEFORE' AND contract.EVENT_MANIPULATION='DELETE'
   AND contract.TRIGGER_NAME='mt5b_membership_delete_contract'
   AND core.TRIGGER_NAME='mt5b_membership_security_delete'
   AND contract.ACTION_ORDER=1 AND core.ACTION_ORDER=2),0,1)
 + ABS(2-(SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()
   AND EVENT_OBJECT_TABLE='institution_membership' AND ACTION_TIMING='BEFORE' AND EVENT_MANIPULATION='DELETE'));
